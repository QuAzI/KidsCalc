using KidAbacusCalculator.Core.Models;

namespace KidAbacusCalculator.Core.Services;

public sealed class AbacusBuilder
{
    public const int PlaceCount = 4;
    public const int MaximumValue = 9_999;

    public IReadOnlyList<AbacusDigit> BuildDigits(int value, int numberOfPlaces = PlaceCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        ArgumentOutOfRangeException.ThrowIfLessThan(numberOfPlaces, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(numberOfPlaces, PlaceCount);

        var maximumValue = Pow10(numberOfPlaces) - 1;
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value, maximumValue);

        var digits = new List<AbacusDigit>(numberOfPlaces);

        // Разряды строятся слева направо: тысячи, сотни, десятки, единицы.
        for (var index = numberOfPlaces - 1; index >= 0; index--)
        {
            var placeValue = Pow10(index);
            digits.Add(new AbacusDigit(placeValue, value / placeValue % 10));
        }

        return digits;
    }

    private static int Pow10(int exponent)
    {
        var result = 1;

        for (var index = 0; index < exponent; index++)
        {
            result *= 10;
        }

        return result;
    }
}
