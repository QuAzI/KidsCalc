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
        ("TaskGenerator не ставит 0 в путающие позиции", TaskGeneratorAvoidsConfusingZeros),
        ("AbacusBuilder раскладывает число", AbacusBuilderSplitsDigits),
        ("MainViewModel проверяет и исправляет ответ", MainViewModelChecksAnswer),
        ("MainViewModel повышает уровень", MainViewModelRaisesDifficulty),
        ("MainViewModel меняет разряды колонками", MainViewModelChangesPlaces),
        ("MainViewModel переносит и занимает разряды", MainViewModelCarriesAndBorrows),
        ("MainViewModel отмечает нужные разряды примера", MainViewModelMarksRelevantPlaces),
        ("MainViewModel принимает числовой ввод", MainViewModelAcceptsNumericInput),
        ("MainViewModel включает звуки бусин и верного ответа", MainViewModelPlaysSounds)
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
        Equal("3 + 4 = 7", addition.SolvedDisplayText);
        Equal(5, subtraction.Answer);
        Equal("7 − 2 = ?", subtraction.DisplayText);
        Equal("7 − 2 = 5", subtraction.SolvedDisplayText);
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

    private static void TaskGeneratorAvoidsConfusingZeros()
    {
        var generator = new TaskGenerator(new Random(42));

        // 0 справа в сложении и любой 0 в вычитании выглядят как «пустой» ход.
        foreach (var maximum in new[] { 1, 10, 20 })
        {
            for (var index = 0; index < 2_000; index++)
            {
                var task = generator.Create(maximum);

                if (task.Operation == MathOperation.Addition)
                {
                    True(
                        task.RightOperand >= 1,
                        $"Сложение с нулём справа: {task.DisplayText}");
                    continue;
                }

                True(
                    task.LeftOperand >= 1 && task.RightOperand >= 1,
                    $"Вычитание с нулём: {task.DisplayText}");
            }
        }
    }

    private static void AbacusBuilderSplitsDigits()
    {
        var builder = new AbacusBuilder();
        var digits = builder.BuildDigits(407);

        Equal(4, digits.Count);
        Equal(new AbacusDigit(1_000, 0), digits[0]);
        Equal(new AbacusDigit(100, 4), digits[1]);
        Equal(new AbacusDigit(10, 0), digits[2]);
        Equal(new AbacusDigit(1, 7), digits[3]);

        var withThousands = builder.BuildDigits(1_407);
        Equal(new AbacusDigit(1_000, 1), withThousands[0]);
        Throws<ArgumentOutOfRangeException>(() => builder.BuildDigits(10_000));
    }

    private static void MainViewModelChecksAnswer()
    {
        var generator = new FakeTaskGenerator(
            new TaskItem(3, 2, MathOperation.Addition));
        var viewModel = new MainViewModel(generator);

        viewModel.CheckAnswerCommand.Execute(null);
        True(viewModel.IsIncorrectFeedback, "Ошибочный ответ не распознан.");
        True(viewModel.FeedbackText.Contains('5'), "Нет подсказки с правильным ответом.");

        viewModel.IncrementPlaceCommand.Execute("1");
        viewModel.IncrementPlaceCommand.Execute("1");

        True(viewModel.IsMatchingAnswer, "Число на счётах не совпало с ответом.");
        False(
            viewModel.IsCorrectFeedback,
            "Ответ засчитан сразу после перебора бусин.");

        viewModel.CheckAnswerCommand.Execute(null);
        True(viewModel.IsCorrectFeedback, "Правильный ответ не распознан.");
        Equal("Верно!", viewModel.PromptText);
        Equal("3 + 2 = 5", viewModel.ProblemText);
        False(
            viewModel.IncrementPlaceCommand.CanExecute("1"),
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

    private static void MainViewModelChangesPlaces()
    {
        var generator = new FakeTaskGenerator(
            new TaskItem(3, 2, MathOperation.Addition));
        var viewModel = new MainViewModel(generator);

        Equal(3, viewModel.CurrentValue);
        False(
            viewModel.DecrementPlaceCommand.CanExecute("100"),
            "Сотни уменьшаются, когда числа меньше 100.");

        viewModel.IncrementPlaceCommand.Execute("100");
        Equal(103, viewModel.CurrentValue);
        viewModel.DecrementPlaceCommand.Execute("100");
        Equal(3, viewModel.CurrentValue);

        viewModel.IncrementPlaceCommand.Execute("10");
        Equal(13, viewModel.CurrentValue);
        viewModel.DecrementPlaceCommand.Execute("10");
        viewModel.IncrementPlaceCommand.Execute("1");
        viewModel.IncrementPlaceCommand.Execute("1");
        Equal(5, viewModel.CurrentValue);
        False(
            viewModel.IsCorrectFeedback,
            "Совпадение засчитано сразу после последней бусины.");
        viewModel.CheckAnswerCommand.Execute(null);
        True(viewModel.IsCorrectFeedback, "Совпадение с ответом не засчитано.");
    }

    private static void MainViewModelCarriesAndBorrows()
    {
        var generator = new FakeTaskGenerator(
            new TaskItem(9, 80, MathOperation.Addition));
        var viewModel = new MainViewModel(generator);

        Equal(9, viewModel.CurrentValue);
        True(
            viewModel.IncrementPlaceCommand.CanExecute("1"),
            "Девять единиц нельзя перенести в десятки.");
        viewModel.IncrementPlaceCommand.Execute("1");
        Equal(10, viewModel.CurrentValue);

        True(
            viewModel.DecrementPlaceCommand.CanExecute("1"),
            "Единицу нельзя занять из десятков.");
        viewModel.DecrementPlaceCommand.Execute("1");
        Equal(9, viewModel.CurrentValue);

        viewModel.IncrementPlaceCommand.Execute("1");
        viewModel.IncrementPlaceCommand.Execute("1000");
        Equal(1_010, viewModel.CurrentValue);
        Equal(1, viewModel.ThousandsDigit);
        Equal(0, viewModel.HundredsDigit);
        Equal(1, viewModel.TensDigit);
        Equal(0, viewModel.OnesDigit);

        True(
            viewModel.DecrementPlaceCommand.CanExecute("100"),
            "Сотни нельзя занять из тысяч.");
        viewModel.DecrementPlaceCommand.Execute("10");
        Equal(1_000, viewModel.CurrentValue);
        viewModel.DecrementPlaceCommand.Execute("1");
        Equal(999, viewModel.CurrentValue);

        False(
            viewModel.IncrementPlaceCommand.CanExecute("10000"),
            "Разряд вне счётов принимается.");
    }

    private static void MainViewModelMarksRelevantPlaces()
    {
        // Учитывается и ответ: перенос из 9 + 8 делает десятки нужными,
        // хотя оба показанных операнда состоят только из единиц.
        var ones = new MainViewModel(
            new FakeTaskGenerator(new TaskItem(3, 2, MathOperation.Addition)));
        var carryToTens = new MainViewModel(
            new FakeTaskGenerator(new TaskItem(9, 8, MathOperation.Addition)));
        var hundreds = new MainViewModel(
            new FakeTaskGenerator(new TaskItem(407, 7, MathOperation.Subtraction)));
        var thousands = new MainViewModel(
            new FakeTaskGenerator(new TaskItem(999, 2, MathOperation.Addition)));

        Equal(1, ones.MaximumTaskPlaceValue);
        False(ones.IsTensColumnRelevant, "Десятки цветные для однозначного примера.");
        False(ones.IsHundredsColumnRelevant, "Сотни цветные для однозначного примера.");
        False(ones.IsThousandsColumnRelevant, "Тысячи цветные для однозначного примера.");

        Equal(10, carryToTens.MaximumTaskPlaceValue);
        True(carryToTens.IsTensColumnRelevant, "Разряд ответа не учтён.");
        Equal(100, hundreds.MaximumTaskPlaceValue);
        True(hundreds.IsHundredsColumnRelevant, "Разряд сотен не учтён.");
        Equal(1_000, thousands.MaximumTaskPlaceValue);
        True(thousands.IsThousandsColumnRelevant, "Разряд тысяч не учтён.");
    }

    private static void MainViewModelPlaysSounds()
    {
        var sounds = new FakeSoundService();
        var generator = new FakeTaskGenerator(
            new TaskItem(3, 2, MathOperation.Addition));
        var viewModel = new MainViewModel(generator, sounds);

        viewModel.IncrementPlaceCommand.Execute("1");
        Equal(1, sounds.BeadCount);
        Equal(0, sounds.CorrectCount);

        viewModel.IncrementPlaceCommand.Execute("1");
        Equal(2, sounds.BeadCount);
        viewModel.CheckAnswerCommand.Execute(null);
        Equal(1, sounds.CorrectCount);
    }

    private static void MainViewModelAcceptsNumericInput()
    {
        var generator = new FakeTaskGenerator(
            new TaskItem(3, 2, MathOperation.Addition));
        var viewModel = new MainViewModel(generator);

        Equal(string.Empty, viewModel.AnswerText);
        viewModel.AnswerText = "1x2";
        Equal("12", viewModel.AnswerText);
        Equal(12, viewModel.CurrentValue);

        viewModel.AnswerText = "12345";
        Equal("1234", viewModel.AnswerText);
        Equal(1_234, viewModel.CurrentValue);

        viewModel.IncrementPlaceCommand.Execute("1");
        Equal(string.Empty, viewModel.AnswerText);
        Equal(1_235, viewModel.CurrentValue);
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

    private sealed class FakeSoundService : ISoundService
    {
        public int BeadCount { get; private set; }

        public int CorrectCount { get; private set; }

        public void PlayBead() => BeadCount++;

        public void PlayCorrect() => CorrectCount++;

        public void WarmUp()
        {
        }
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
