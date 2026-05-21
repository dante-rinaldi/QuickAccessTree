using Microsoft.Win32;
using System.Windows;
using System.Windows.Media;
using SidebarBuddy.Models;

namespace SidebarBuddy.Services;

public static class ThemeManager
{
    public static void Apply(ThemeMode mode)
    {
        bool dark = mode switch
        {
            ThemeMode.Dark  => true,
            ThemeMode.Light => false,
            _               => IsSystemDark()
        };

        var r = Application.Current.Resources;
        if (dark)
        {
            Brush(r, "Theme.SidebarBg",     0x1E, 0x1E, 0x1E);
            Brush(r, "Theme.HeaderBg",      0x25, 0x25, 0x26);
            Brush(r, "Theme.PopupBg",       0x2D, 0x2D, 0x30);
            Brush(r, "Theme.BorderBrush",   0x3C, 0x3C, 0x3C);
            Brush(r, "Theme.BorderSoft",    0x55, 0x55, 0x55);
            Brush(r, "Theme.ItemHover",     0x2D, 0x2D, 0x30);
            Brush(r, "Theme.ItemSelect",    0x37, 0x37, 0x3D);
            Brush(r, "Theme.PrimaryText",   0xDC, 0xDC, 0xDC);
            Brush(r, "Theme.SecondaryText", 0xCC, 0xCC, 0xCC);
            Brush(r, "Theme.DimText",       0x70, 0x70, 0x70);
            Brush(r, "Theme.ScrollThumb",   0x55, 0x55, 0x55);
        }
        else
        {
            Brush(r, "Theme.SidebarBg",     0xF5, 0xF5, 0xF5);
            Brush(r, "Theme.HeaderBg",      0xEB, 0xEB, 0xEB);
            Brush(r, "Theme.PopupBg",       0xFF, 0xFF, 0xFF);
            Brush(r, "Theme.BorderBrush",   0xD0, 0xD0, 0xD0);
            Brush(r, "Theme.BorderSoft",    0xBB, 0xBB, 0xBB);
            Brush(r, "Theme.ItemHover",     0xE0, 0xE0, 0xE0);
            Brush(r, "Theme.ItemSelect",    0xCE, 0xCE, 0xD8);
            Brush(r, "Theme.PrimaryText",   0x1A, 0x1A, 0x1A);
            Brush(r, "Theme.SecondaryText", 0x33, 0x33, 0x33);
            Brush(r, "Theme.DimText",       0x77, 0x77, 0x77);
            Brush(r, "Theme.ScrollThumb",   0xAA, 0xAA, 0xAA);
        }
    }

    private static void Brush(ResourceDictionary r, string key, byte red, byte grn, byte blu)
        => r[key] = new SolidColorBrush(Color.FromRgb(red, grn, blu));

    public static bool IsSystemDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return (int)(key?.GetValue("AppsUseLightTheme") ?? 1) == 0;
        }
        catch { return true; }
    }
}
