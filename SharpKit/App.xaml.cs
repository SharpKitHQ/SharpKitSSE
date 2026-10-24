using System.Configuration;
using System.Data;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using ArgsParser;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using SharpKit.Data;
using Application = System.Windows.Application;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using MessageBox = System.Windows.MessageBox;

namespace SharpKit;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private IHost? _host;
    
    public static IServiceProvider? ServiceProvider { get; set; }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            var splash = new Splash();
            splash.Show();

            var parser = new Parser(e.Args)
                .SupportsOption<string?>("plugin", "Directly load a plugin on start")
                .ShowHelpLegend(true)
                .Help(2, "Usage:")
                .Parse();

            if (parser.HasErrors)
            {
                parser.ShowErrors(2, "Issues:");
                throw new ArgumentException();
            }

            var argContainer = new ArgContainer
            {
                LoadPluginName = parser.GetOption<string?>("plugin")
            };
            
            var builder = Host.CreateApplicationBuilder();

            var appDataPath =  Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var fullAppDataPath = Path.Combine(appDataPath, ".SharpKit");
            
            if(!Directory.Exists(fullAppDataPath))
                Directory.CreateDirectory(fullAppDataPath);
            
            var configFilePath = Path.Combine(fullAppDataPath, "sharpkit.json");
            if (!File.Exists(configFilePath))
            {
                if(File.Exists("sharpkit.default.json"))
                    File.Copy("sharpkit.default.json", configFilePath);
            }

            builder.Configuration.SetBasePath(fullAppDataPath);
            builder.Configuration.AddJsonFile("sharpkit.json", false, true);
            
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(builder.Configuration)
                .CreateLogger();
            
            builder.Logging.ClearProviders();
            builder.Logging.AddSerilog();

            builder.Services.AddSingleton(argContainer);
            DIConfig.SetupDI(builder.Services);

            _host = builder.Build();
            ServiceProvider = _host.Services;
            
            await _host.StartAsync();

            _ = _host.Services.GetRequiredService<PluginFileManager>();
            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            var logger = _host.Services.GetRequiredService<ILogger<App>>();
            
            logger.LogInformation("Application starting...");

            splash.Close();
            MainWindow = mainWindow;
            mainWindow.Show();

            if (SystemInformation.TerminalServerSession)
            {
                MessageBox.Show("You seem to be using remote desktop (Microsoft RDP, VNC, etc.).\n\nPlease keep in mind this is not fully supported and some features (especially the 3D window) might not work correctly.\n\nYou can disable this warning in the settings.", "Warning",  MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Host terminated unexpectedly");
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        try
        {
            if (_host is not null)
            {
                await _host.StopAsync();
                _host.Dispose();
            }
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Host terminated unexpectedly");
        }
        finally
        {
            await Log.CloseAndFlushAsync();
            base.OnExit(e);
        }
        
    }
}