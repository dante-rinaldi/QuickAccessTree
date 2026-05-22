using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;
using SidebarBuddy.Models;

namespace SidebarBuddy.Services;

public class SettingsService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SidebarBuddy", "settings.json");

    private const string StartupKeyPath  = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupAppName  = "SidebarBuddy";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters    = { new JsonStringEnumConverter() }
    };

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var s = JsonSerializer.Deserialize<AppSettings>(
                            File.ReadAllText(SettingsPath), Options)
                        ?? new AppSettings();
                // JSON deserialization produces a case-sensitive dict; re-wrap so path
                // lookups are case-insensitive (Windows paths are case-insensitive).
                s.FolderColors = new Dictionary<string, string>(s.FolderColors, StringComparer.OrdinalIgnoreCase);
                return s;
            }
        }
        catch { }
        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, Options));
        }
        catch { }
    }

    public bool GetLaunchOnStartup()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupKeyPath, writable: false);
            return key?.GetValue(StartupAppName) != null;
        }
        catch { return false; }
    }

    public void SetLaunchOnStartup(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupKeyPath, writable: true);
            if (key == null) return;
            if (enable)
            {
                string exe = Environment.ProcessPath
                             ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
                key.SetValue(StartupAppName, $"\"{exe}\"");
            }
            else
            {
                key.DeleteValue(StartupAppName, throwOnMissingValue: false);
            }
        }
        catch { }
    }
}
