using System.IO;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace SidebarBuddy.Services;

public static class LicenseService
{
    private const string ServerBase  = "https://sidebarbuddy.com";
    private const string RegKeyPath  = @"SOFTWARE\SidebarBuddy";
    private const string DeviceIdKey = "DeviceId";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(12) };

    // ── Device fingerprint ─────────────────────────────────────────────────

    public static string GetDeviceId()
    {
        // Registry is the primary store — survives app reinstalls
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegKeyPath, writable: false);
            if (key?.GetValue(DeviceIdKey) is string stored && stored.Length >= 32)
                return stored;
        }
        catch { }

        var parts = new List<string> { Environment.MachineName };
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            if (key?.GetValue("MachineGuid") is string guid) parts.Add(guid);
        }
        catch { }
        try { parts.Add(GetMacAddress()); } catch { }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("|", parts)));
        var id   = Convert.ToHexString(hash).ToLowerInvariant()[..40];

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RegKeyPath);
            key?.SetValue(DeviceIdKey, id);
        }
        catch { }

        return id;
    }

    public static string GetMacAddress()
    {
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.NetworkInterfaceType is NetworkInterfaceType.Ethernet or NetworkInterfaceType.Wireless80211
                && ni.OperationalStatus == OperationalStatus.Up)
            {
                var bytes = ni.GetPhysicalAddress().GetAddressBytes();
                if (bytes.Length == 6)
                    return string.Join(":", bytes.Select(b => b.ToString("x2")));
            }
        }
        return string.Empty;
    }

    // ── Trial check ────────────────────────────────────────────────────────

    /// <summary>
    /// Contacts the server to get the authoritative trial state for this device.
    /// Returns (DaysRemaining, StartDate) on success, null on network failure.
    /// </summary>
    public static async Task<(int DaysRemaining, DateTime StartDate)?> CheckTrialAsync()
    {
        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                device_id   = GetDeviceId(),
                mac_address = GetMacAddress()
            });
            var resp = await Http.PostAsync(
                ServerBase + "/trial_status.php",
                new StringContent(payload, Encoding.UTF8, "application/json"));

            resp.EnsureSuccessStatusCode();
            var body = JsonSerializer.Deserialize<JsonElement>(
                await resp.Content.ReadAsStringAsync());

            int      days  = body.GetProperty("days_remaining").GetInt32();
            DateTime start = DateTime.Parse(body.GetProperty("trial_start_date").GetString()!);
            return (days, start);
        }
        catch { return null; }
    }

    // ── License validation ─────────────────────────────────────────────────

    /// <summary>
    /// Validates email + key against the server.
    /// Returns (true, null) on success or (false, errorMessage) on failure.
    /// </summary>
    public static async Task<(bool Valid, string? Error)> ValidateLicenseAsync(
        string email, string key)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                email,
                key,
                device_id   = GetDeviceId(),
                mac_address = GetMacAddress(),
                hostname    = Environment.MachineName
            });
            var resp = await Http.PostAsync(
                ServerBase + "/validate_license.php",
                new StringContent(payload, Encoding.UTF8, "application/json"));

            var body = JsonSerializer.Deserialize<JsonElement>(
                await resp.Content.ReadAsStringAsync());

            bool valid = body.GetProperty("valid").GetBoolean();
            string? error = null;
            if (!valid && body.TryGetProperty("error", out var e))
                error = e.GetString();
            return (valid, error ?? "Validation failed. Please check your email and key.");
        }
        catch
        {
            return (false, "Could not reach the activation server. Check your internet connection.");
        }
    }
}
