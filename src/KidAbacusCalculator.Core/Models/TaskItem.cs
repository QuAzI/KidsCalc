namespace KidAbacusCalculator.Core.Models;

public sealed record TaskItem(
    int LeftOperand,
    int RightOperand,
    MathOperation Operation)
{
    public int Answer => Operation == MathOperation.Addition
        ? LeftOperand + RightOperand
        : LeftOperand - RightOperand;

    public string OperationSymbol => Operation == MathOperation.Addition ? "+" : "−";

    public string DisplayText => FormatDisplay("?");

    public string SolvedDisplayText => FormatDisplay(Answer.ToString());

    private string FormatDisplay(string result) =>
        $"{LeftOperand} {OperationSymbol} {RightOperand} = {result}";
}
