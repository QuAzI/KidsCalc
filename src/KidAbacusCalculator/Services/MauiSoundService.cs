using KidAbacusCalculator.Core.Services;

namespace KidAbacusCalculator.Services;

public sealed class MauiSoundService : ISoundService
{
    private const int SampleRate = 44_100;
    private readonly byte[] _warmupSound;
    private readonly byte[] _beadSound;
    private readonly byte[] _correctSound;
    private readonly object _playGate = new();
    private bool _warmedUp;

#if WINDOWS
    private Windows.Media.Playback.MediaPlayer? _mediaPlayer;
    private Windows.Storage.Streams.InMemoryRandomAccessStream? _mediaStream;
    private Windows.Storage.Streams.InMemoryRandomAccessStream? _previousStream;
#endif

#if ANDROID
    private Android.Media.AudioTrack? _audioTrack;
#endif

    public MauiSoundService()
    {
        var warmupPcm = ToneMixer.ToPcm(ToneMixer.CreateSilence(SampleRate, 80));
        var beadPcm = ToneMixer.ToPcm(ToneMixer.CreateBeadMove(SampleRate));
        var correctPcm = ToneMixer.ToPcm(
            ToneMixer.Concat(
                ToneMixer.CreatePlink(SampleRate, frequencyHz: 392.00, durationMs: 140, peak: 0.09),
                ToneMixer.CreateSilence(SampleRate, durationMs: 20),
                ToneMixer.CreatePlink(SampleRate, frequencyHz: 523.25, durationMs: 220, peak: 0.10)));

#if WINDOWS
        _warmupSound = ToneMixer.WrapWav(warmupPcm, SampleRate);
        _beadSound = ToneMixer.WrapWav(beadPcm, SampleRate);
        _correctSound = ToneMixer.WrapWav(correctPcm, SampleRate);
#else
        _warmupSound = warmupPcm;
        _beadSound = beadPcm;
        _correctSound = correctPcm;
#endif
    }

    public void WarmUp()
    {
        if (_warmedUp)
        {
            return;
        }

        _warmedUp = true;
        Play(_warmupSound, volume: 0.001);
    }

    public void PlayBead() => Play(_beadSound, volume: 0.40);

    public void PlayCorrect() => Play(_correctSound, volume: 0.30);

    private void Play(byte[] sound, double volume)
    {
        try
        {
#if WINDOWS
            _ = PlayWindowsAsync(sound, volume);
#elif ANDROID
            _ = Task.Run(() => PlayAndroid(sound, (float)volume));
#endif
        }
        catch
        {
            // Звук не должен прерывать игру, если устройство его не воспроизвело.
        }
    }

#if WINDOWS
    private async Task PlayWindowsAsync(byte[] wav, double volume)
    {
        var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
        var writer = new Windows.Storage.Streams.DataWriter(stream);
        writer.WriteBytes(wav);
        await writer.StoreAsync();
        writer.DetachStream();
        writer.Dispose();
        stream.Seek(0);

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            lock (_playGate)
            {
                EnsureWindowsPlayer();
                _mediaPlayer!.Pause();
                _previousStream?.Dispose();
                _previousStream = _mediaStream;
                _mediaStream = stream;
                _mediaPlayer.Volume = volume;
                _mediaPlayer.Source = Windows.Media.Core.MediaSource.CreateFromStream(
                    stream,
                    "audio/wav");
            }
        });
    }

    private void EnsureWindowsPlayer()
    {
        if (_mediaPlayer is not null)
        {
            return;
        }

        _mediaPlayer = new Windows.Media.Playback.MediaPlayer
        {
            AutoPlay = true,
            AudioCategory = Windows.Media.Playback.MediaPlayerAudioCategory.SoundEffects
        };
    }
#endif

#if ANDROID
    private void PlayAndroid(byte[] pcm, float volume)
    {
        lock (_playGate)
        {
            try
            {
                PlayAndroidLocked(pcm, volume);
            }
            catch
            {
                ReleaseAndroidTrack();
            }
        }
    }

    // STREAM не стартует, пока не заполнен аппаратный буфер: щелчок бусины ~40 мс
    // короче этого буфера, поэтому Write заканчивался, а динамик молчал.
    // STATIC загружает весь PCM и сразу Play. 44100 Гц — родная частота большинства устройств.
    private void PlayAndroidLocked(byte[] pcm, float volume)
    {
        var minBuffer = Android.Media.AudioTrack.GetMinBufferSize(
            SampleRate,
            Android.Media.ChannelOut.Mono,
            Android.Media.Encoding.Pcm16bit);
        if (minBuffer <= 0)
        {
            minBuffer = pcm.Length;
        }

        var attributes = new Android.Media.AudioAttributes.Builder()
            .SetUsage(Android.Media.AudioUsageKind.Media)
            .SetContentType(Android.Media.AudioContentType.Music)
            .Build();
        var format = new Android.Media.AudioFormat.Builder()
            .SetEncoding(Android.Media.Encoding.Pcm16bit)
            .SetSampleRate(SampleRate)
            .SetChannelMask(Android.Media.ChannelOut.Mono)
            .Build();
        if (attributes is null || format is null)
        {
            return;
        }

        ReleaseAndroidTrack();
        _audioTrack = new Android.Media.AudioTrack.Builder()
            .SetAudioAttributes(attributes)
            .SetAudioFormat(format)
            .SetBufferSizeInBytes(Math.Max(minBuffer, pcm.Length))
            .SetTransferMode(Android.Media.AudioTrackMode.Static)
            .Build();

        if (_audioTrack is null || _audioTrack.State != Android.Media.AudioTrackState.Initialized)
        {
            ReleaseAndroidTrack();
            return;
        }

        _audioTrack.SetVolume(Math.Clamp(volume, 0f, 1f));
        var written = _audioTrack.Write(pcm, 0, pcm.Length, Android.Media.WriteMode.Blocking);
        if (written <= 0)
        {
            ReleaseAndroidTrack();
            return;
        }

        _audioTrack.Play();
    }

    private void ReleaseAndroidTrack()
    {
        if (_audioTrack is null)
        {
            return;
        }

        try
        {
            if (_audioTrack.PlayState != Android.Media.PlayState.Stopped)
            {
                _audioTrack.Stop();
            }
        }
        catch
        {
        }

        _audioTrack.Release();
        _audioTrack = null;
    }
#endif

    private static class ToneMixer
    {
        public static double[] CreateSilence(int sampleRate, int durationMs) =>
            new double[Samples(sampleRate, durationMs)];

        // Сухой щелчок двух костяшек: короткий удар без ноты и без шипения.
        public static double[] CreateBeadMove(int sampleRate)
        {
            var samples = new double[Samples(sampleRate, 42)];
            var seed = 4_177;
            AddBeadClick(samples, sampleRate, offset: 0, gain: 1.00, ref seed);
            AddBeadClick(samples, sampleRate, offset: Samples(sampleRate, 9), gain: 0.28, ref seed);
            return samples;
        }

        private static void AddBeadClick(
            double[] samples,
            int sampleRate,
            int offset,
            double gain,
            ref int seed)
        {
            var length = Samples(sampleRate, 24);
            double low = 0;
            double band = 0;

            for (var index = 0; index < length && offset + index < samples.Length; index++)
            {
                var time = index / (double)sampleRate;
                var noise = NextNoise(ref seed);
                low += 0.22 * (noise - low);
                var high = noise - low;
                band += 0.62 * (high - band);

                var tick = time < 0.0018
                    ? band * (1d - (time / 0.0018)) * 0.07
                    : 0d;
                var shell = (Math.Sin(2d * Math.PI * 1_050 * time) * Math.Exp(-110 * time) * 0.09)
                    + (Math.Sin(2d * Math.PI * 1_720 * time) * Math.Exp(-150 * time) * 0.04);

                samples[offset + index] += gain * (tick + shell);
            }
        }

        public static double[] CreatePlink(
            int sampleRate,
            double frequencyHz,
            int durationMs,
            double peak)
        {
            var sampleCount = Samples(sampleRate, durationMs);
            var samples = new double[sampleCount];
            var attack = Math.Min(Math.Max(1, sampleRate * 14 / 1_000), sampleCount / 4);
            var release = Math.Min(Math.Max(1, sampleRate * 40 / 1_000), sampleCount / 2);

            for (var index = 0; index < sampleCount; index++)
            {
                var envelope = 1d;
                if (index < attack)
                {
                    envelope = index / (double)attack;
                }
                else if (index >= sampleCount - release)
                {
                    envelope = (sampleCount - index) / (double)release;
                }

                var decay = Math.Exp(-3.2 * index / sampleCount);
                samples[index] = Math.Sin(2d * Math.PI * frequencyHz * index / sampleRate)
                    * peak
                    * envelope
                    * decay;
            }

            return samples;
        }

        public static double[] Concat(params double[][] parts)
        {
            var length = 0;
            foreach (var part in parts)
            {
                length += part.Length;
            }

            var result = new double[length];
            var offset = 0;
            foreach (var part in parts)
            {
                Array.Copy(part, 0, result, offset, part.Length);
                offset += part.Length;
            }

            return result;
        }

        public static byte[] ToPcm(double[] samples)
        {
            var pcm = new byte[samples.Length * 2];
            for (var index = 0; index < samples.Length; index++)
            {
                var clamped = Math.Clamp(samples[index], -1d, 1d);
                var value = (short)Math.Round(clamped * short.MaxValue);
                pcm[index * 2] = (byte)(value & 0xFF);
                pcm[(index * 2) + 1] = (byte)((value >> 8) & 0xFF);
            }

            return pcm;
        }

        public static byte[] WrapWav(byte[] pcm, int sampleRate)
        {
            var wav = new byte[44 + pcm.Length];
            WriteAscii(wav, 0, "RIFF");
            WriteInt32(wav, 4, wav.Length - 8);
            WriteAscii(wav, 8, "WAVE");
            WriteAscii(wav, 12, "fmt ");
            WriteInt32(wav, 16, 16);
            WriteInt16(wav, 20, 1);
            WriteInt16(wav, 22, 1);
            WriteInt32(wav, 24, sampleRate);
            WriteInt32(wav, 28, sampleRate * 2);
            WriteInt16(wav, 32, 2);
            WriteInt16(wav, 34, 16);
            WriteAscii(wav, 36, "data");
            WriteInt32(wav, 40, pcm.Length);
            Buffer.BlockCopy(pcm, 0, wav, 44, pcm.Length);
            return wav;
        }

        private static int Samples(int sampleRate, int durationMs) =>
            Math.Max(1, sampleRate * durationMs / 1_000);

        private static double NextNoise(ref int seed)
        {
            seed = (1_103_515_245 * seed) + 12_345;
            return ((seed & 0x7FFF_FFFF) / (double)0x4000_0000) - 1d;
        }

        private static void WriteAscii(byte[] buffer, int offset, string text)
        {
            for (var index = 0; index < text.Length; index++)
            {
                buffer[offset + index] = (byte)text[index];
            }
        }

        private static void WriteInt16(byte[] buffer, int offset, short value)
        {
            buffer[offset] = (byte)(value & 0xFF);
            buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
        }

        private static void WriteInt32(byte[] buffer, int offset, int value)
        {
            buffer[offset] = (byte)(value & 0xFF);
            buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
            buffer[offset + 2] = (byte)((value >> 16) & 0xFF);
            buffer[offset + 3] = (byte)((value >> 24) & 0xFF);
        }
    }
}
