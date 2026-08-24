using System.ComponentModel;
using KidAbacusCalculator.Core.Services;
using KidAbacusCalculator.Core.ViewModels;

namespace KidAbacusCalculator;

public partial class MainPage : ContentPage
{
    private readonly MainViewModel _viewModel;
    private readonly ISoundService _soundService;
    private IDispatcherTimer? _answerCheckTimer;
    private IDispatcherTimer? _nextTaskTimer;

    public MainPage(MainViewModel viewModel, ISoundService soundService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _soundService = soundService;
        BindingContext = viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.BeadsMoved += OnBeadsMoved;
        _viewModel.AnswerInputChanged += OnAnswerInputChanged;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, EventArgs eventArgs)
    {
        Loaded -= OnLoaded;
        _soundService.WarmUp();
    }

    private void OnBeadsMoved(object? sender, EventArgs eventArgs)
    {
        // Быстрый перебор бусин не должен сразу засчитывать ответ:
        // проверка только после паузы после последнего движения.
        ScheduleAnswerCheck();
    }

    private void OnAnswerInputChanged(object? sender, EventArgs eventArgs)
    {
        // Очистка поля означает, что ответ ещё не введён; иначе используем
        // ту же паузу, что и для бусин, чтобы набор нескольких цифр не прерывался.
        if (string.IsNullOrEmpty(_viewModel.AnswerText))
        {
            CancelAnswerCheck();
            return;
        }

        ScheduleAnswerCheck();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName != nameof(MainViewModel.FeedbackState))
        {
            return;
        }

        // SemanticProperties не предоставляет live-region в MAUI XAML,
        // поэтому динамический результат явно передаётся экранному диктору.
        if (_viewModel.IsCorrectFeedback)
        {
            Dispatcher.Dispatch(
                () => SemanticScreenReader.Announce(
                    $"{_viewModel.PromptText}. {_viewModel.ProblemText}"));
            CancelAnswerCheck();
            ScheduleNextTask();
        }
        else
        {
            CancelNextTask();
        }
    }

    private void ScheduleAnswerCheck()
    {
        CancelAnswerCheck();
        _answerCheckTimer = Dispatcher.CreateTimer();
        _answerCheckTimer.Interval = TimeSpan.FromSeconds(2);
        _answerCheckTimer.IsRepeating = false;
        _answerCheckTimer.Tick += OnAnswerCheckTimerTick;
        _answerCheckTimer.Start();
    }

    private void OnAnswerCheckTimerTick(object? sender, EventArgs eventArgs)
    {
        CancelAnswerCheck();
        if (_viewModel.IsMatchingAnswer && _viewModel.CheckAnswerCommand.CanExecute(null))
        {
            _viewModel.CheckAnswerCommand.Execute(null);
        }
    }

    private void CancelAnswerCheck()
    {
        if (_answerCheckTimer is null)
        {
            return;
        }

        _answerCheckTimer.Stop();
        _answerCheckTimer.Tick -= OnAnswerCheckTimerTick;
        _answerCheckTimer = null;
    }

    private void ScheduleNextTask()
    {
        CancelNextTask();
        _nextTaskTimer = Dispatcher.CreateTimer();
        _nextTaskTimer.Interval = TimeSpan.FromMilliseconds(2_800);
        _nextTaskTimer.IsRepeating = false;
        _nextTaskTimer.Tick += OnNextTaskTimerTick;
        _nextTaskTimer.Start();
    }

    private void OnNextTaskTimerTick(object? sender, EventArgs eventArgs)
    {
        CancelNextTask();
        _viewModel.NewTaskCommand.Execute(null);
    }

    private void CancelNextTask()
    {
        if (_nextTaskTimer is null)
        {
            return;
        }

        _nextTaskTimer.Stop();
        _nextTaskTimer.Tick -= OnNextTaskTimerTick;
        _nextTaskTimer = null;
    }
}
