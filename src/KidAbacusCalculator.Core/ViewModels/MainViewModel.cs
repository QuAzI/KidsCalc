using System.Windows.Input;
using KidAbacusCalculator.Core.Models;
using KidAbacusCalculator.Core.Services;

namespace KidAbacusCalculator.Core.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly ITaskGenerator _taskGenerator;
    private readonly ISoundService? _soundService;
    private TaskItem _currentTask = null!;
    private int _currentValue;
    private int _difficultyLimit = 10;
    private int _consecutiveCorrectAnswers;
    private int _mistakesAtCurrentLevel;
    private bool _incorrectCountedForTask;
    private FeedbackState _feedbackState;
    private string _feedbackText = string.Empty;
    private string _answerText = string.Empty;

    public MainViewModel(ITaskGenerator taskGenerator, ISoundService? soundService = null)
    {
        _taskGenerator = taskGenerator ?? throw new ArgumentNullException(nameof(taskGenerator));
        _soundService = soundService;

        IncrementPlaceCommand = new RelayCommand(
            parameter => ChangePlace(parameter, 1),
            parameter => CanChangePlace(ParsePlace(parameter), 1));
        DecrementPlaceCommand = new RelayCommand(
            parameter => ChangePlace(parameter, -1),
            parameter => CanChangePlace(ParsePlace(parameter), -1));
        CheckAnswerCommand = new RelayCommand(
            CheckAnswer,
            () => FeedbackState != FeedbackState.Correct);
        NewTaskCommand = new RelayCommand(CreateNewTask);

        CreateNewTask();
    }

    public TaskItem CurrentTask
    {
        get => _currentTask;
        private set
        {
            if (SetProperty(ref _currentTask, value))
            {
                OnPropertyChanged(nameof(ProblemText));
                OnPropertyChanged(nameof(ProblemPrefix));
                OnPropertyChanged(nameof(IsMatchingAnswer));
            }
        }
    }

    public int CurrentValue
    {
        get => _currentValue;
        private set
        {
            if (SetProperty(ref _currentValue, value))
            {
                OnPropertyChanged(nameof(AbacusDescription));
                OnPropertyChanged(nameof(IsMatchingAnswer));
                // Счётчики разрядов находятся в отдельных элементах над кнопками,
                // поэтому каждый из них должен обновляться вместе с общим числом.
                OnPropertyChanged(nameof(ThousandsDigit));
                OnPropertyChanged(nameof(HundredsDigit));
                OnPropertyChanged(nameof(TensDigit));
                OnPropertyChanged(nameof(OnesDigit));
                NotifyCommandStates();
            }
        }
    }

    public int DifficultyLimit
    {
        get => _difficultyLimit;
        private set => SetProperty(ref _difficultyLimit, value);
    }

    public FeedbackState FeedbackState
    {
        get => _feedbackState;
        private set
        {
            if (SetProperty(ref _feedbackState, value))
            {
                OnPropertyChanged(nameof(IsFeedbackVisible));
                OnPropertyChanged(nameof(IsCorrectFeedback));
                OnPropertyChanged(nameof(IsIncorrectFeedback));
                OnPropertyChanged(nameof(ProblemText));
                OnPropertyChanged(nameof(PromptText));
                NotifyCommandStates();
            }
        }
    }

    public string FeedbackText
    {
        get => _feedbackText;
        private set => SetProperty(ref _feedbackText, value);
    }

    public string AnswerText
    {
        get => _answerText;
        set
        {
            // Ручной ввод синхронизирует счёты с полем ответа, но пустое поле
            // оставляет текущее положение бусин и снова показывает placeholder.
            var normalized = NormalizeAnswerText(value);
            if (!SetProperty(ref _answerText, normalized))
            {
                return;
            }

            if (int.TryParse(normalized, out var enteredValue))
            {
                FeedbackState = FeedbackState.None;
                FeedbackText = string.Empty;
                CurrentValue = enteredValue;
            }

            AnswerInputChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public string ProblemText => IsCorrectFeedback
        ? CurrentTask.SolvedDisplayText
        : CurrentTask.DisplayText;

    public string ProblemPrefix =>
        $"{CurrentTask.LeftOperand} {CurrentTask.OperationSymbol} {CurrentTask.RightOperand} =";

    public string PromptText => IsCorrectFeedback ? "Верно!" : "Реши пример";

    public string AbacusDescription => $"На счётах число {CurrentValue}";

    public int ThousandsDigit => (CurrentValue / 1_000) % 10;

    public int HundredsDigit => (CurrentValue / 100) % 10;

    public int TensDigit => (CurrentValue / 10) % 10;

    public int OnesDigit => CurrentValue % 10;

    public bool IsMatchingAnswer => CurrentValue == CurrentTask.Answer;

    public bool IsFeedbackVisible => FeedbackState != FeedbackState.None;

    public bool IsCorrectFeedback => FeedbackState == FeedbackState.Correct;

    public bool IsIncorrectFeedback => FeedbackState == FeedbackState.Incorrect;

    public ICommand IncrementPlaceCommand { get; }

    public ICommand DecrementPlaceCommand { get; }

    public ICommand CheckAnswerCommand { get; }

    public ICommand NewTaskCommand { get; }

    public event EventHandler? BeadsMoved;

    public event EventHandler? AnswerInputChanged;

    private void ChangePlace(object? parameter, int direction)
    {
        var placeValue = ParsePlace(parameter);
        if (!CanChangePlace(placeValue, direction))
        {
            return;
        }

        FeedbackState = FeedbackState.None;
        FeedbackText = string.Empty;
        ClearAnswerInput();
        CurrentValue += placeValue * direction;
        BeadsMoved?.Invoke(this, EventArgs.Empty);
        _soundService?.PlayBead();
    }

    // Перенос и заём идут через всё число: 9+1 в единицах даёт 10,
    // а вычесть единицу из 10 можно за счёт старшего разряда.
    private bool CanChangePlace(int placeValue, int direction)
    {
        if (FeedbackState == FeedbackState.Correct)
        {
            return false;
        }

        if (placeValue is not (1 or 10 or 100 or 1_000) || direction is not (1 or -1))
        {
            return false;
        }

        if (direction > 0)
        {
            return CurrentValue + placeValue <= AbacusBuilder.MaximumValue;
        }

        return CurrentValue >= placeValue;
    }

    private static int ParsePlace(object? parameter)
    {
        if (parameter is int placeValue)
        {
            return placeValue;
        }

        return int.TryParse(parameter?.ToString(), out var parsed) ? parsed : 0;
    }

    private static string NormalizeAnswerText(string? value) =>
        new(
            (value ?? string.Empty)
                .Where(character => character is >= '0' and <= '9')
                .Take(4)
                .ToArray());

    private void CheckAnswer()
    {
        // Серия ответов повышает уровень только один раз, а ошибка в одном
        // примере не должна многократно понижать сложность при повторной проверке.
        if (CurrentValue == CurrentTask.Answer)
        {
            _consecutiveCorrectAnswers++;
            _mistakesAtCurrentLevel = 0;

            if (_consecutiveCorrectAnswers >= 3 && DifficultyLimit == 10)
            {
                DifficultyLimit = 20;
                FeedbackText = "Верно! Открыт уровень до 20 🎉";
            }
            else
            {
                FeedbackText = "Верно! Отличная работа! 🎉";
            }

            FeedbackState = FeedbackState.Correct;
            SetProperty(
                ref _answerText,
                CurrentTask.Answer.ToString(),
                nameof(AnswerText));
            _soundService?.PlayCorrect();
            return;
        }

        _consecutiveCorrectAnswers = 0;

        if (!_incorrectCountedForTask)
        {
            _incorrectCountedForTask = true;
            _mistakesAtCurrentLevel++;
        }

        if (_mistakesAtCurrentLevel >= 2 && DifficultyLimit == 20)
        {
            DifficultyLimit = 10;
            _mistakesAtCurrentLevel = 0;
        }

        FeedbackText = $"Почти! Попробуй получить {CurrentTask.Answer}.";
        FeedbackState = FeedbackState.Incorrect;
    }

    private void CreateNewTask()
    {
        CurrentTask = _taskGenerator.Create(DifficultyLimit);
        _incorrectCountedForTask = false;
        FeedbackText = string.Empty;
        FeedbackState = FeedbackState.None;
        ClearAnswerInput();
        CurrentValue = CurrentTask.LeftOperand;
    }

    private void ClearAnswerInput()
    {
        if (SetProperty(ref _answerText, string.Empty, nameof(AnswerText)))
        {
            AnswerInputChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void NotifyCommandStates()
    {
        ((RelayCommand)IncrementPlaceCommand).NotifyCanExecuteChanged();
        ((RelayCommand)DecrementPlaceCommand).NotifyCanExecuteChanged();
        ((RelayCommand)CheckAnswerCommand).NotifyCanExecuteChanged();
    }
}
