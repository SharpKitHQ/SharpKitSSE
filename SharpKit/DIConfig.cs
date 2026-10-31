using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
        services.AddSingleton<CellEditor>();
        services.AddSingleton<CellExplorer>();

        services.AddSingleton(sp =>
        {
            var log = sp.GetRequiredService<ILogger<CachedBSAFileProvider>>();
            var pfm = sp.GetRequiredService<PluginFileManager>();

            var dataDirPath = pfm.GetEnv().DataFolderPath;
            
            var provider = new CachedBSAFileProvider(log, dataDirPath);
            provider.OpenArchive(Path.Combine(dataDirPath, "Skyrim - Meshes0.bsa"));
            provider.OpenArchive(Path.Combine(dataDirPath, "Skyrim - Meshes1.bsa"));
            provider.OpenArchive(Path.Combine(dataDirPath, "Skyrim - Textures0.bsa"));
            provider.OpenArchive(Path.Combine(dataDirPath, "Skyrim - Textures1.bsa"));
            provider.OpenArchive(Path.Combine(dataDirPath, "Skyrim - Textures2.bsa"));
            provider.OpenArchive(Path.Combine(dataDirPath, "Skyrim - Textures3.bsa"));
            provider.OpenArchive(Path.Combine(dataDirPath, "Skyrim - Textures4.bsa"));
            provider.OpenArchive(Path.Combine(dataDirPath, "Skyrim - Textures5.bsa"));
            provider.OpenArchive(Path.Combine(dataDirPath, "Skyrim - Textures6.bsa"));
            provider.OpenArchive(Path.Combine(dataDirPath, "Skyrim - Textures7.bsa"));
            provider.OpenArchive(Path.Combine(dataDirPath, "Skyrim - Textures8.bsa"));

            return provider;
        });
    }
}
