using System.ComponentModel;
using KidAbacusCalculator.Core.ViewModels;

namespace KidAbacusCalculator;

public partial class MainPage : ContentPage
{
    private readonly MainViewModel _viewModel;

    public MainPage(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        // SemanticProperties не предоставляет live-region в MAUI XAML,
        // поэтому динамический результат явно передаётся экранному диктору.
        if (eventArgs.PropertyName == nameof(MainViewModel.FeedbackState)
            && _viewModel.IsFeedbackVisible)
        {
            Dispatcher.Dispatch(
                () => SemanticScreenReader.Announce(_viewModel.FeedbackText));
        }
    }
}
