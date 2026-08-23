using System.Windows.Input;
using KidAbacusCalculator.Core.Models;
using KidAbacusCalculator.Core.Services;

namespace KidAbacusCalculator.Core.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private const int MaximumAbacusValue = 999;
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

        IncrementCommand = new RelayCommand(
            () => ChangeValue(1),
            () => CurrentValue < MaximumAbacusValue && FeedbackState != FeedbackState.Correct);
        DecrementCommand = new RelayCommand(
            () => ChangeValue(-1),
            () => CurrentValue > 0 && FeedbackState != FeedbackState.Correct);
        ResetCommand = new RelayCommand(
            ResetValue,
            () => FeedbackState != FeedbackState.Correct);
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
                NotifyCommandStates();
            }
        }
    }

    public int DifficultyLimit
    {
        get => _difficultyLimit;
        private set
        {
            if (SetProperty(ref _difficultyLimit, value))
            {
                OnPropertyChanged(nameof(DifficultyText));
            }
        }
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
                NotifyCommandStates();
            }
        }
    }

    public string FeedbackText
    {
        get => _feedbackText;
        private set => SetProperty(ref _feedbackText, value);
    }

    public string ProblemText => CurrentTask.DisplayText;

    public string DifficultyText => $"Уровень: до {DifficultyLimit}";

    public string AbacusDescription => $"На счётах число {CurrentValue}";

    public bool IsFeedbackVisible => FeedbackState != FeedbackState.None;

    public bool IsCorrectFeedback => FeedbackState == FeedbackState.Correct;

    public bool IsIncorrectFeedback => FeedbackState == FeedbackState.Incorrect;

    public ICommand IncrementCommand { get; }

    public ICommand DecrementCommand { get; }

    public ICommand ResetCommand { get; }

    public ICommand CheckAnswerCommand { get; }

    public ICommand NewTaskCommand { get; }

    private void ChangeValue(int delta)
    {
        FeedbackState = FeedbackState.None;
        FeedbackText = string.Empty;
        CurrentValue = Math.Clamp(CurrentValue + delta, 0, MaximumAbacusValue);
    }

    private void ResetValue()
    {
        FeedbackState = FeedbackState.None;
        FeedbackText = string.Empty;
        CurrentValue = CurrentTask.LeftOperand;
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
        ((RelayCommand)IncrementCommand).NotifyCanExecuteChanged();
        ((RelayCommand)DecrementCommand).NotifyCanExecuteChanged();
        ((RelayCommand)ResetCommand).NotifyCanExecuteChanged();
        ((RelayCommand)CheckAnswerCommand).NotifyCanExecuteChanged();
    }
}
