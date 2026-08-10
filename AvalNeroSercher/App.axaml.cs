using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AvalNeroSercher.ViewModels;
using AvalNeroSercher.Views;
using AvalNeroSercher.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AvalNeroSercher;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var services = new ServiceCollection();
            services.AddSingleton<CameraService>();
            services.AddSingleton<Predictor>(_ =>
                new Predictor("E:\\С++\\models\\Dise.zip")); // путь к модели
            services.AddSingleton<MainViewModel>();

            var provider = services.BuildServiceProvider();

            desktop.MainWindow = new MainWindow
            {
                DataContext = provider.GetRequiredService<MainViewModel>()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}