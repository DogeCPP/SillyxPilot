using System;
using System.IO;

namespace SillyxPilot.Plugin;

public static class AppPaths
{
    private static string? _pluginDir;
    private static string? _dataRoot;

    public static void Init(string pluginDir)
    {
        _pluginDir = pluginDir;
        var parent = Path.GetDirectoryName(pluginDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (!string.IsNullOrEmpty(parent)) _dataRoot = parent;
    }

    public static string XpilotDataRoot()
    {
        if (!string.IsNullOrEmpty(_dataRoot)) return _dataRoot!;
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "org.vatsim.xpilot");
    }

    public static string XpilotSoundsDir() => Path.Combine(XpilotDataRoot(), "Sounds");

    public static string SillyDataDir()
    {
        var dir = Path.Combine(XpilotDataRoot(), "SillyxPilot");
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static string PluginDir() => _pluginDir ?? AppContext.BaseDirectory;

    public static string WwwRoot() => Path.Combine(PluginDir(), "SillyxPilot", "wwwroot");
}
