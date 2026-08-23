using System.ComponentModel;
using KidAbacusCalculator.Core.ViewModels;

namespace KidAbacusCalculator;

public partial class MainPage : ContentPage
{
    private readonly MainViewModel _viewModel;
    private IDispatcherTimer? _nextTaskTimer;

    public MainPage(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName != nameof(MainViewModel.FeedbackState))
        {
            return;
        }

        // SemanticProperties не предоставляет live-region в MAUI XAML,
        // поэтому динамический результат явно передаётся экранному диктору.
        if (_viewModel.IsFeedbackVisible)
        {
            Dispatcher.Dispatch(
                () => SemanticScreenReader.Announce(_viewModel.FeedbackText));
        }

        // Без кнопки «Новый» следующий пример открывается сам после верного ответа.
        if (_viewModel.IsCorrectFeedback)
        {
            ScheduleNextTask();
        }
        else
        {
            CancelNextTask();
        }
    }

    private void ScheduleNextTask()
    {
        CancelNextTask();
        _nextTaskTimer = Dispatcher.CreateTimer();
        _nextTaskTimer.Interval = TimeSpan.FromMilliseconds(1_400);
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
