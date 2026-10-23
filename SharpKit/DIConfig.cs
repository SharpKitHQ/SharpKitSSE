using Microsoft.Extensions.DependencyInjection;
using SharpKit.Reflection;
using SharpKit.Util;
using SharpKit.Windows;
using SharpKit.Windows.Data;

namespace SharpKit;

public static class DIConfig
{
    public static void SetupDI(IServiceCollection services)
    {
        services.AddSingleton<PluginFileManager>();
        services.AddTransient<DataFileSelectorWindow>();
        services.AddTransient<WindowPosConfigManager>();

        services.AddSingleton<MainWindowVM>();
        services.AddSingleton<MainWindow>();
        services.AddSingleton<EditorProvider>();
    }
}