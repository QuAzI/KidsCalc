namespace KidAbacusCalculator.Core.Models;

public sealed record AbacusDigit(int PlaceValue, int Value)
{
    public int NumericValue => PlaceValue * Value;
}
