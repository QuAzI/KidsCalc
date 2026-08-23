namespace KidAbacusCalculator;

public partial class App : Application
{
    private readonly MainPage _mainPage;

    public App(MainPage mainPage)
    {
        InitializeComponent();
        _mainPage = mainPage;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // Портретное окно под телефон: те же размеры, что у Shopper MainWindow
        // (480×820, минимум 420×640), вместо широкого десктопного кадра MAUI.
        return new Window(_mainPage)
        {
            Title = "Счёты",
            Width = 480,
            Height = 820,
            MinimumWidth = 420,
            MinimumHeight = 640
        };
    }
}