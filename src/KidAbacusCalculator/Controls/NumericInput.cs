namespace KidAbacusCalculator.Controls;

public sealed class NumericInput : Entry
{
    private bool _isNormalizingText;

    public NumericInput()
    {
        Keyboard = Keyboard.Numeric;
        Placeholder = "?";
        MaxLength = 4;
        TextChanged += OnTextChanged;
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        // Нативные Entry добавляют нижнюю линию или рамку даже при прозрачном
        // фоне; убираем это оформление, сохраняя ввод и фокусировку.
#if ANDROID
        if (Handler?.PlatformView is Android.Widget.EditText platformInput)
        {
            platformInput.BackgroundTintList =
                Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
        }
#elif WINDOWS
        if (Handler?.PlatformView is Microsoft.UI.Xaml.Controls.TextBox platformInput)
        {
            platformInput.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
        }
#endif
    }

    private void OnTextChanged(object? sender, TextChangedEventArgs eventArgs)
    {
        if (_isNormalizingText)
        {
            return;
        }

        // Numeric-клавиатура всё равно может отдавать знак и разделитель,
        // поэтому контрол пропускает только цифры, допустимые четырьмя спицами.
        var normalized = new string(
            (eventArgs.NewTextValue ?? string.Empty)
                .Where(character => character is >= '0' and <= '9')
                .Take(MaxLength)
                .ToArray());
        if (normalized == eventArgs.NewTextValue)
        {
            return;
        }

        _isNormalizingText = true;
        Text = normalized;
        CursorPosition = normalized.Length;
        _isNormalizingText = false;
    }
}
