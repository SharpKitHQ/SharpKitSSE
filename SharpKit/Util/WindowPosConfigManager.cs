using System.IO;
using System.Numerics;

namespace SharpKit.Util;

public class WindowPosConfigManager
{
    public struct WindowPosConfig
    {
        public Vector4 RenderWindowPos { get; set; }
        public Vector4 RecordWindowPos { get; set; }
        public Vector4 CellWindowPos { get; set; }
    }

    public WindowPosConfig Config { get; private set; } = new();

    public bool IsLoaded { get; private set; } = false;
    
    private static string DataDirPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".SharpKit");
    private static string IniFilePath = Path.Combine(DataDirPath, "WindowPositions.ini");

    public void UpdateRenderWindowPos(int left, int width, int top, int height)
    {
        Config = Config with
        {
            RenderWindowPos = new Vector4(left, width, top, height)
        };
        Save(Config);
    }

    public void UpdateRecordWindowPos(int left, int width, int top, int height)
    {
        Config = Config with
        {
            RecordWindowPos = new Vector4(left, width, top, height)
        };
        Save(Config);
    }

    public void UpdateCellWindowPos(int left, int width, int top, int height)
    {
        Config = Config with
        {
            CellWindowPos = new Vector4(left, width, top, height)
        };
        Save(Config);
    }
    
    public WindowPosConfigManager()
    {
        if (!File.Exists(IniFilePath))
        {
            return;
        }

        var ini = new IniFile();
        ini.Load(IniFilePath);

        Config = new WindowPosConfig { 
            RenderWindowPos = new Vector4(
                ini["RenderWindow"]["Left"].ToInt(),
                ini["RenderWindow"]["Width"].ToInt(),
                ini["RenderWindow"]["Top"].ToInt(),
                ini["RenderWindow"]["Height"].ToInt()
            ), 
            RecordWindowPos = new Vector4(
                ini["RecordWindow"]["Left"].ToInt(),
                ini["RecordWindow"]["Width"].ToInt(),
                ini["RecordWindow"]["Top"].ToInt(),
                ini["RecordWindow"]["Height"].ToInt()
            ), 
            CellWindowPos = new Vector4(
                ini["CellWindow"]["Left"].ToInt(),
                ini["CellWindow"]["Width"].ToInt(),
                ini["CellWindow"]["Top"].ToInt(),
                ini["CellWindow"]["Height"].ToInt()
            )
        };

        IsLoaded = true;
    }

    public void Save(WindowPosConfig config)
    {
        Config = config;
        IsLoaded = true;
        
        var ini = new IniFile();
        
        ini["RenderWindow"]["Left"] = config.RenderWindowPos.X;
        ini["RenderWindow"]["Width"] = config.RenderWindowPos.Y;
        ini["RenderWindow"]["Top"] = config.RenderWindowPos.Z;
        ini["RenderWindow"]["Height"] = config.RenderWindowPos.W;
        
        ini["RecordWindow"]["Left"] = config.RecordWindowPos.X;
        ini["RecordWindow"]["Width"] = config.RecordWindowPos.Y;
        ini["RecordWindow"]["Top"] = config.RecordWindowPos.Z;
        ini["RecordWindow"]["Height"] = config.RecordWindowPos.W;
        
        ini["CellWindow"]["Left"] = config.CellWindowPos.X;
        ini["CellWindow"]["Width"] = config.CellWindowPos.Y;
        ini["CellWindow"]["Top"] = config.CellWindowPos.Z;
        ini["CellWindow"]["Height"] = config.CellWindowPos.W;
        
        ini.Save(IniFilePath);
    }
}