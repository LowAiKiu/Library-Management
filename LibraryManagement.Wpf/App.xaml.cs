using LibraryManagement.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Windows;

namespace LibraryManagement.Wpf;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    public static IHost HostContainer { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddJsonFile("appsettings.json", optional: true);
        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddSingleton<MainWindow>();
        HostContainer = builder.Build();
        HostContainer.Start();
        using (var scope = HostContainer.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
            SeedData.Initialize(db);
        }

        var mainWindow = HostContainer.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await HostContainer.StopAsync();
        HostContainer.Dispose();
        base.OnExit(e);
    }
}

