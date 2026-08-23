using KidAbacusCalculator.Core.Models;

namespace KidAbacusCalculator.Core.Services;

public sealed class TaskGenerator : ITaskGenerator
{
    private readonly Random _random;

    public TaskGenerator()
        : this(Random.Shared)
    {
    }

    public TaskGenerator(Random random)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
    }

    public TaskItem Create(int maximumAnswer)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumAnswer, 1);
        // Тысячный ряд на счётах есть, но примеры его не используют.
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maximumAnswer, 999);

        var operation = _random.Next(2) == 0
            ? MathOperation.Addition
            : MathOperation.Subtraction;

        // Границы подбираются отдельно: сумма не превышает уровень,
        // а вычитание всегда даёт неотрицательный результат.
        if (operation == MathOperation.Addition)
        {
            var left = _random.Next(maximumAnswer + 1);
            var right = _random.Next(maximumAnswer - left + 1);
            return new TaskItem(left, right, operation);
        }

        var minuend = _random.Next(maximumAnswer + 1);
        var subtrahend = _random.Next(minuend + 1);
        return new TaskItem(minuend, subtrahend, operation);
    }
}
