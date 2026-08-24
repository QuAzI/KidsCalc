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
