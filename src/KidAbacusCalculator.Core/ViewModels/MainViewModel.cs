using System.Windows.Input;
using KidAbacusCalculator.Core.Models;
using KidAbacusCalculator.Core.Services;

namespace KidAbacusCalculator.Core.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly ITaskGenerator _taskGenerator;
    private TaskItem _currentTask = null!;
    private int _currentValue;
    private int _difficultyLimit = 10;
    private int _consecutiveCorrectAnswers;
    private int _mistakesAtCurrentLevel;
    private bool _incorrectCountedForTask;
    private FeedbackState _feedbackState;
    private string _feedbackText = string.Empty;

    public MainViewModel(ITaskGenerator taskGenerator)
    {
        _taskGenerator = taskGenerator ?? throw new ArgumentNullException(nameof(taskGenerator));

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

    public string ProblemText => IsCorrectFeedback
        ? CurrentTask.SolvedDisplayText
        : CurrentTask.DisplayText;

    public string PromptText => IsCorrectFeedback ? "Верно!" : "Реши пример";

    public string AbacusDescription => $"На счётах число {CurrentValue}";

    public bool IsMatchingAnswer => CurrentValue == CurrentTask.Answer;

    public bool IsFeedbackVisible => FeedbackState != FeedbackState.None;

    public bool IsCorrectFeedback => FeedbackState == FeedbackState.Correct;

    public bool IsIncorrectFeedback => FeedbackState == FeedbackState.Incorrect;

    public ICommand IncrementPlaceCommand { get; }

    public ICommand DecrementPlaceCommand { get; }

    public ICommand CheckAnswerCommand { get; }

    public ICommand NewTaskCommand { get; }

    public event EventHandler? BeadsMoved;

    private void ChangePlace(object? parameter, int direction)
    {
        var placeValue = ParsePlace(parameter);
        if (!CanChangePlace(placeValue, direction))
        {
            return;
        }

        FeedbackState = FeedbackState.None;
        FeedbackText = string.Empty;
        CurrentValue += placeValue * direction;
        BeadsMoved?.Invoke(this, EventArgs.Empty);
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
        CurrentValue = CurrentTask.LeftOperand;
    }

    private void NotifyCommandStates()
    {
        ((RelayCommand)IncrementPlaceCommand).NotifyCanExecuteChanged();
        ((RelayCommand)DecrementPlaceCommand).NotifyCanExecuteChanged();
        ((RelayCommand)CheckAnswerCommand).NotifyCanExecuteChanged();
    }
}
