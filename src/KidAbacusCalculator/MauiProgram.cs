using KidAbacusCalculator.Core.Services;
using KidAbacusCalculator.Core.ViewModels;
using KidAbacusCalculator.Services;

namespace KidAbacusCalculator;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        builder.Services.AddSingleton<ITaskGenerator, TaskGenerator>();
        builder.Services.AddSingleton<ISoundService, MauiSoundService>();
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<MainPage>();

        return builder.Build();
    }
}
