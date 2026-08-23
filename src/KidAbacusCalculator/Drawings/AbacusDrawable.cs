using KidAbacusCalculator.Core.Services;
using Microsoft.Maui.Graphics;

namespace KidAbacusCalculator.Drawings;

public sealed class AbacusDrawable : IDrawable
{
    private const int BeadsPerRod = 9;
    private static readonly Color[] ActiveColors =
    [
        Color.FromArgb("#2563EB"),
        Color.FromArgb("#7C3AED"),
        Color.FromArgb("#F97316"),
        Color.FromArgb("#0F9D78")
    ];

    private readonly AbacusBuilder _abacusBuilder;
    private int _previousValue;
    private int _targetValue;
    private float _progress = 1f;

    public AbacusDrawable(AbacusBuilder abacusBuilder)
    {
        _abacusBuilder = abacusBuilder;
    }

    public void SetTransition(int previousValue, int targetValue, double progress)
    {
        _previousValue = Math.Clamp(previousValue, 0, AbacusBuilder.MaximumValue);
        _targetValue = Math.Clamp(targetValue, 0, AbacusBuilder.MaximumValue);
        _progress = (float)Math.Clamp(progress, 0d, 1d);
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (dirtyRect.Width <= 0 || dirtyRect.Height <= 0)
        {
            return;
        }

        canvas.SaveState();
        canvas.Antialias = true;

        var previousDigits = _abacusBuilder.BuildDigits(_previousValue);
        var targetDigits = _abacusBuilder.BuildDigits(_targetValue);
        var rodTop = 16f;
        var rodBottom = Math.Max(rodTop + 100f, dirtyRect.Height - 54f);
        var beadRadius = Math.Clamp(dirtyRect.Width / 24f, 8f, 16f);
        var beadDiameter = beadRadius * 2f;
        var step = Math.Min(
            beadDiameter * 0.78f,
            (rodBottom - rodTop) / (BeadsPerRod + 1));
        var rodCount = targetDigits.Count;

        // Каждый разряд рисуется одинаково, но позиции бусин интерполируются
        // между старой и новой цифрой — это сохраняет смысл движения при анимации.
        for (var rodIndex = 0; rodIndex < rodCount; rodIndex++)
        {
            var centerX = dirtyRect.Width * (rodIndex + 0.5f) / rodCount;
            var previousDigit = previousDigits[rodIndex].Value;
            var targetDigit = targetDigits[rodIndex].Value;

            DrawRod(
                canvas,
                centerX,
                rodTop,
                rodBottom,
                beadRadius,
                step,
                previousDigit,
                targetDigit,
                ActiveColors[rodIndex]);
        }

        canvas.RestoreState();
    }

    private void DrawRod(
        ICanvas canvas,
        float centerX,
        float rodTop,
        float rodBottom,
        float beadRadius,
        float step,
        int previousDigit,
        int targetDigit,
        Color activeColor)
    {
        canvas.StrokeColor = Color.FromArgb("#475569");
        canvas.StrokeSize = 4f;
        canvas.DrawLine(centerX, rodTop - 8f, centerX, rodBottom + 8f);

        // Активные бусины собираются сверху, остальные — снизу.
        // Между группами остаётся зазор, поэтому цифра читается без текста.
        for (var beadIndex = 0; beadIndex < BeadsPerRod; beadIndex++)
        {
            var previousY = GetBeadY(beadIndex, previousDigit, rodTop, rodBottom, step);
            var targetY = GetBeadY(beadIndex, targetDigit, rodTop, rodBottom, step);
            var currentY = previousY + ((targetY - previousY) * _progress);
            var isActive = _progress < 0.5f
                ? beadIndex < previousDigit
                : beadIndex < targetDigit;

            canvas.FillColor = isActive ? activeColor : Color.FromArgb("#E2E8F0");
            canvas.StrokeColor = isActive
                ? Color.FromArgb("#1E293B")
                : Color.FromArgb("#64748B");
            canvas.StrokeSize = 1.5f;
            canvas.FillEllipse(
                centerX - beadRadius,
                currentY - beadRadius,
                beadRadius * 2f,
                beadRadius * 2f);
            canvas.DrawEllipse(
                centerX - beadRadius,
                currentY - beadRadius,
                beadRadius * 2f,
                beadRadius * 2f);
        }

        canvas.FillColor = activeColor;
        canvas.FillRoundedRectangle(centerX - 20f, rodBottom + 16f, 40f, 32f, 10f);
        canvas.FontColor = Colors.White;
        canvas.FontSize = 18f;
        canvas.DrawString(
            targetDigit.ToString(),
            centerX - 20f,
            rodBottom + 16f,
            40f,
            32f,
            HorizontalAlignment.Center,
            VerticalAlignment.Center);
    }

    private static float GetBeadY(
        int beadIndex,
        int digit,
        float rodTop,
        float rodBottom,
        float step)
    {
        return beadIndex < digit
            ? rodTop + (beadIndex * step)
            : rodBottom - ((BeadsPerRod - 1 - beadIndex) * step);
    }
}
