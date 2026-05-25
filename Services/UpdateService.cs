using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace SidebarBuddy.Services;

public record UpdateInfo(bool Available, string CurrentVersion, string LatestVersion, string? DownloadUrl);

public static class UpdateService
{
    private const string ReleasesApi =
        "https://api.github.com/repos/dante-rinaldi/sidebarbuddy-releases/releases/latest";

    public static string CurrentVersion
    {
        get
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            return v != null ? $"{v.Major}.{v.Minor}.{v.Build}" : "0.0.0";
        }
    }

    public static async Task<UpdateInfo> CheckAsync()
    {
        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("SidebarBuddy");
            http.Timeout = TimeSpan.FromSeconds(10);

            var json = await http.GetStringAsync(ReleasesApi);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tag = root.GetProperty("tag_name").GetString()?.TrimStart('v') ?? "0.0.0";

            string? downloadUrl = null;
            if (root.TryGetProperty("assets", out var assets))
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.GetProperty("name").GetString() ?? "";
                    if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        downloadUrl = asset.GetProperty("browser_download_url").GetString();
                        break;
                    }
                }
            }

            var current = new Version(CurrentVersion);
            var latest = new Version(tag);

            return new UpdateInfo(latest > current, CurrentVersion, tag, downloadUrl);
        }
        catch
        {
            return new UpdateInfo(false, CurrentVersion, CurrentVersion, null);
        }
    }
}
