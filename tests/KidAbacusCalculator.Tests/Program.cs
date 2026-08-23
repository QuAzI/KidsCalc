using KidAbacusCalculator.Core.Models;
using KidAbacusCalculator.Core.Services;
using KidAbacusCalculator.Core.ViewModels;

namespace KidAbacusCalculator.Tests;

internal static class Program
{
    private static readonly (string Name, Action Test)[] Tests =
    [
        ("TaskItem вычисляет операции", TaskItemCalculatesOperations),
        ("TaskGenerator соблюдает границы", TaskGeneratorKeepsAnswersInRange),
        ("AbacusBuilder раскладывает число", AbacusBuilderSplitsDigits),
        ("MainViewModel проверяет и исправляет ответ", MainViewModelChecksAnswer),
        ("MainViewModel повышает уровень", MainViewModelRaisesDifficulty)
    ];

    public static int Main()
    {
        var failed = 0;

        // Каждый тест выполняется независимо: одна ошибка не скрывает результаты
        // остальных проверок, а код возврата остаётся пригодным для CI.
        foreach (var (name, test) in Tests)
        {
            try
            {
                test();
                Console.WriteLine($"PASS: {name}");
            }
            catch (Exception exception)
            {
                failed++;
                Console.Error.WriteLine($"FAIL: {name}");
                Console.Error.WriteLine(exception.Message);
            }
        }

        Console.WriteLine($"Итого: {Tests.Length - failed}/{Tests.Length} тестов пройдено.");
        return failed == 0 ? 0 : 1;
    }

    private static void TaskItemCalculatesOperations()
    {
        var addition = new TaskItem(3, 4, MathOperation.Addition);
        var subtraction = new TaskItem(7, 2, MathOperation.Subtraction);

        Equal(7, addition.Answer);
        Equal("3 + 4 = ?", addition.DisplayText);
        Equal(5, subtraction.Answer);
        Equal("7 − 2 = ?", subtraction.DisplayText);
    }

    private static void TaskGeneratorKeepsAnswersInRange()
    {
        var generator = new TaskGenerator(new Random(42));

        // Большая выборка проверяет обе операции и ключевые инварианты:
        // диапазон ответа и отсутствие отрицательного результата.
        foreach (var maximum in new[] { 10, 20 })
        {
            for (var index = 0; index < 2_000; index++)
            {
                var task = generator.Create(maximum);

                True(task.LeftOperand >= 0, "Левый операнд отрицательный.");
                True(task.RightOperand >= 0, "Правый операнд отрицательный.");
                True(task.Answer is >= 0, "Ответ отрицательный.");
                True(task.Answer <= maximum, "Ответ превышает уровень.");

                if (task.Operation == MathOperation.Subtraction)
                {
                    True(
                        task.LeftOperand >= task.RightOperand,
                        "Вычитание создаёт отрицательный ответ.");
                }
            }
        }
    }

    private static void AbacusBuilderSplitsDigits()
    {
        var builder = new AbacusBuilder();
        var digits = builder.BuildDigits(407);

        Equal(3, digits.Count);
        Equal(new AbacusDigit(100, 4), digits[0]);
        Equal(new AbacusDigit(10, 0), digits[1]);
        Equal(new AbacusDigit(1, 7), digits[2]);
        Throws<ArgumentOutOfRangeException>(() => builder.BuildDigits(1_000));
    }

    private static void MainViewModelChecksAnswer()
    {
        var generator = new FakeTaskGenerator(
            new TaskItem(3, 2, MathOperation.Addition));
        var viewModel = new MainViewModel(generator);

        viewModel.CheckAnswerCommand.Execute(null);
        True(viewModel.IsIncorrectFeedback, "Ошибочный ответ не распознан.");
        True(viewModel.FeedbackText.Contains('5'), "Нет подсказки с правильным ответом.");

        viewModel.IncrementCommand.Execute(null);
        viewModel.IncrementCommand.Execute(null);
        viewModel.CheckAnswerCommand.Execute(null);

        True(viewModel.IsCorrectFeedback, "Правильный ответ не распознан.");
        False(
            viewModel.IncrementCommand.CanExecute(null),
            "После правильного ответа значение всё ещё можно менять.");
    }

    private static void MainViewModelRaisesDifficulty()
    {
        var generator = new FakeTaskGenerator(
            new TaskItem(1, 0, MathOperation.Addition),
            new TaskItem(2, 0, MathOperation.Addition),
            new TaskItem(3, 0, MathOperation.Addition),
            new TaskItem(10, 5, MathOperation.Addition));
        var viewModel = new MainViewModel(generator);

        for (var answer = 0; answer < 3; answer++)
        {
            viewModel.CheckAnswerCommand.Execute(null);

            if (answer < 2)
            {
                viewModel.NewTaskCommand.Execute(null);
            }
        }

        Equal(20, viewModel.DifficultyLimit);
        viewModel.NewTaskCommand.Execute(null);
        Equal(20, generator.RequestedLimits[^1]);
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException(
                $"Ожидалось: {expected}; получено: {actual}.");
        }
    }

    private static void True(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void False(bool condition, string message) => True(!condition, message);

    private static void Throws<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Ожидалось исключение {typeof(TException).Name}.");
    }

    private sealed class FakeTaskGenerator(params TaskItem[] tasks) : ITaskGenerator
    {
        private readonly Queue<TaskItem> _tasks = new(tasks);

        public List<int> RequestedLimits { get; } = [];

        public TaskItem Create(int maximumAnswer)
        {
            RequestedLimits.Add(maximumAnswer);
            return _tasks.Count > 0
                ? _tasks.Dequeue()
                : new TaskItem(0, 0, MathOperation.Addition);
        }
    }
}
