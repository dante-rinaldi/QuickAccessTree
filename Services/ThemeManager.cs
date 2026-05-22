using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using SidebarBuddy.Interop;
using SidebarBuddy.Models;

namespace SidebarBuddy.Services;

public static class ThemeManager
{
    // Set once in App.xaml.cs after the MainWindow HWND is available.
    public static nint WindowHandle { get; set; }

    // ── Light / Dark solid colors ─────────────────────────────────────

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

        // Ensure customization resources exist with safe defaults
        if (!r.Contains("Theme.SidebarOpacity"))  r["Theme.SidebarOpacity"]   = 1.0;
        if (!r.Contains("Theme.BgImageSource"))   r["Theme.BgImageSource"]    = null;
        if (!r.Contains("Theme.BgImageOpacity"))  r["Theme.BgImageOpacity"]   = 0.35;
        if (!r.Contains("Theme.BgImageVisible"))  r["Theme.BgImageVisible"]   = Visibility.Collapsed;
        if (!r.Contains("Theme.TextGlowEffect"))  r["Theme.TextGlowEffect"]   = null;
    }

    // ── Skin defaults ─────────────────────────────────────────────────

    // Returns (bgOpacity, textGlow, glowIntensity) for skins that have preset defaults.
    // Returns null for solid/no-texture skins (SolidDark, SolidLight, HighContrast).
    public static (double BgOpacity, bool Glow, double GlowIntensity)? GetSkinDefault(AppSkin skin)
        => skin switch
        {
            AppSkin.FrostedGlass => (0.20, true, 0.55),
            AppSkin.Mica         => (0.90, true, 0.10),
            AppSkin.NeonCyber    => (0.40, true, 0.10),
            AppSkin.Terminal     => (0.60, true, 0.80),
            AppSkin.Paper        => (1.00, true, 0.10),
            AppSkin.Synthwave    => (0.25, true, 0.30),
            AppSkin.BrushedMetal => (0.55, true, 0.10),
            AppSkin.Custom       => (0.25, true, 0.10),
            _                    => null,
        };

    // ── Customization ─────────────────────────────────────────────────

    private static readonly Dictionary<AppSkin, string> PresetTextures = new()
    {
        [AppSkin.Terminal]     = "terminal.png",
        [AppSkin.Synthwave]    = "synthwave.png",
        [AppSkin.BrushedMetal] = "brushedmetal.png",
        [AppSkin.NeonCyber]    = "neoncyber.png",
        [AppSkin.Paper]        = "paper.png",
        [AppSkin.FrostedGlass] = "frostedglass.png",
        [AppSkin.Mica]         = "mica.png",
    };

    private static string TexturesFolder =>
        Path.Combine(
            Path.GetDirectoryName(
                System.Reflection.Assembly.GetExecutingAssembly().Location)!,
            "Textures");

    public static void ApplyAppearance(AppSettings settings)
    {
        var r = Application.Current.Resources;

        // Sidebar opacity
        r["Theme.SidebarOpacity"] = settings.SidebarOpacity;

        // Highlight color override
        if (!string.IsNullOrEmpty(settings.HighlightColor))
        {
            try
            {
                var c = (Color)ColorConverter.ConvertFromString(settings.HighlightColor);
                r["Theme.ItemHover"]  = new SolidColorBrush(Color.FromArgb(180, c.R, c.G, c.B));
                r["Theme.ItemSelect"] = new SolidColorBrush(Color.FromArgb(210, c.R, c.G, c.B));
            }
            catch { }
        }

        // Text glow
        r["Theme.TextGlowEffect"] = settings.TextGlow
            ? (object)new DropShadowEffect
              {
                  Color         = Colors.White,
                  BlurRadius    = Math.Round(10.0 * settings.TextGlowIntensity, 1),
                  ShadowDepth   = 0,
                  Opacity       = settings.TextGlowIntensity,
                  RenderingBias = RenderingBias.Performance,
              }
            : null;

        // Background image
        if (!settings.ShowBackgroundImage)
        {
            r["Theme.BgImageSource"]  = null;
            r["Theme.BgImageVisible"] = Visibility.Collapsed;
            return;
        }

        string? imagePath = null;
        if (settings.Skin == AppSkin.Custom)
        {
            // User-supplied image
            if (!string.IsNullOrEmpty(settings.CustomImagePath) &&
                File.Exists(settings.CustomImagePath))
                imagePath = settings.CustomImagePath;
        }
        else if (PresetTextures.TryGetValue(settings.Skin, out var texFile))
        {
            // Preset texture shipped with the app
            string candidate = Path.Combine(TexturesFolder, texFile);
            if (File.Exists(candidate)) imagePath = candidate;
        }

        if (imagePath != null)
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource   = new Uri(imagePath, UriKind.Absolute);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                r["Theme.BgImageSource"]  = bmp;
                r["Theme.BgImageOpacity"] = settings.BackgroundImageOpacity;
                r["Theme.BgImageVisible"] = Visibility.Visible;
            }
            catch
            {
                r["Theme.BgImageSource"]  = null;
                r["Theme.BgImageVisible"] = Visibility.Collapsed;
            }
        }
        else
        {
            r["Theme.BgImageSource"]  = null;
            r["Theme.BgImageVisible"] = Visibility.Collapsed;
        }
    }

    // ── Skins ─────────────────────────────────────────────────────────

    public static void ApplySkin(AppSkin skin)
    {
        var r = Application.Current.Resources;

        switch (skin)
        {
            case AppSkin.SolidDark:
                ClearAcrylic();
                Apply(ThemeMode.Dark);
                break;
            case AppSkin.SolidLight:
                ClearAcrylic();
                Apply(ThemeMode.Light);
                break;

            case AppSkin.FrostedGlass:
                // Subtle blue gradient: transparent at top, soft blue tint at bottom.
                r["Theme.SidebarBg"] = new LinearGradientBrush(
                    new GradientStopCollection
                    {
                        new GradientStop(Color.FromArgb(12,  0x05, 0x10, 0x30), 0.0), // top: near-invisible
                        new GradientStop(Color.FromArgb(80,  0x0A, 0x28, 0x80), 1.0), // bottom: soft blue
                    },
                    new System.Windows.Point(0, 0),
                    new System.Windows.Point(0, 1));
                BrushA(r, "Theme.HeaderBg",      168, 0x07, 0x18, 0x38); // dark navy blue, semi-transparent
                BrushA(r, "Theme.PopupBg",       230, 0x18, 0x20, 0x30); // popups stay readable
                Brush (r, "Theme.BorderBrush",   0x5A, 0xB8, 0xFF);      // light blue stroke
                Brush (r, "Theme.BorderSoft",    0x30, 0x78, 0xAA);      // softer blue
                BrushA(r, "Theme.ItemHover",     210, 0x25, 0x65, 0xB8); // vivid cobalt hover
                BrushA(r, "Theme.ItemSelect",    225, 0x2A, 0x72, 0xCC); // vivid cobalt select
                Brush (r, "Theme.PrimaryText",   0xE8, 0xF0, 0xFF);      // slightly cool white
                Brush (r, "Theme.SecondaryText", 0xCC, 0xD8, 0xF0);
                Brush (r, "Theme.DimText",       0x68, 0x88, 0xA8);
                Brush (r, "Theme.ScrollThumb",   0x38, 0x70, 0x99);
                // Tint: AABBGGRR = 0x401C100A (~25% dark blue tint)
                ApplyAcrylic(unchecked((int)0x401C100A));
                break;

            case AppSkin.Mica:
                // Light neutral semi-transparent — simulates Mica (real DWM Mica needs
                // a non-layered window; this is the best we can do with AllowsTransparency=True).
                BrushA(r, "Theme.SidebarBg",     220, 0xF5, 0xF5, 0xF8);
                BrushA(r, "Theme.HeaderBg",      230, 0xED, 0xED, 0xF0);
                BrushA(r, "Theme.PopupBg",       245, 0xFF, 0xFF, 0xFF);
                Brush (r, "Theme.BorderBrush",   0xC0, 0xC0, 0xC8);
                Brush (r, "Theme.BorderSoft",    0xB0, 0xB0, 0xB8);
                BrushA(r, "Theme.ItemHover",     180, 0xE0, 0xE0, 0xE8);
                BrushA(r, "Theme.ItemSelect",    200, 0xCC, 0xCC, 0xDA);
                Brush (r, "Theme.PrimaryText",   0xFF, 0xFF, 0xFF);
                Brush (r, "Theme.SecondaryText", 0xDD, 0xDD, 0xDD);
                Brush (r, "Theme.DimText",       0x99, 0x99, 0x99);
                Brush (r, "Theme.ScrollThumb",   0xAA, 0xAA, 0xAA);
                // Tint: AABBGGRR = 0x90F8F5F5 (56% light neutral)
                ApplyAcrylic(unchecked((int)0x90F8F5F5));
                break;

            case AppSkin.NeonCyber:
                ClearAcrylic();
                Brush(r, "Theme.SidebarBg",     0x0A, 0x0E, 0x14);
                Brush(r, "Theme.HeaderBg",      0x0D, 0x11, 0x17);
                Brush(r, "Theme.PopupBg",       0x16, 0x1B, 0x22);
                Brush(r, "Theme.BorderBrush",   0x00, 0x88, 0xAA);
                Brush(r, "Theme.BorderSoft",    0x1A, 0x30, 0x40);
                Brush(r, "Theme.ItemHover",     0x0D, 0x1F, 0x2D);
                Brush(r, "Theme.ItemSelect",    0x16, 0x32, 0x40);
                Brush(r, "Theme.PrimaryText",   0x00, 0xFF, 0xFF);
                Brush(r, "Theme.SecondaryText", 0x80, 0xDF, 0xFF);
                Brush(r, "Theme.DimText",       0x30, 0x50, 0x60);
                Brush(r, "Theme.ScrollThumb",   0x00, 0x44, 0x55);
                break;

            case AppSkin.Terminal:
                ClearAcrylic();
                Brush(r, "Theme.SidebarBg",     0x0C, 0x0C, 0x0C);
                Brush(r, "Theme.HeaderBg",      0x11, 0x11, 0x11);
                Brush(r, "Theme.PopupBg",       0x1A, 0x1A, 0x1A);
                Brush(r, "Theme.BorderBrush",   0x00, 0x88, 0x00);
                Brush(r, "Theme.BorderSoft",    0x1A, 0x2A, 0x1A);
                Brush(r, "Theme.ItemHover",     0x0A, 0x1F, 0x0A);
                Brush(r, "Theme.ItemSelect",    0x14, 0x33, 0x14);
                Brush(r, "Theme.PrimaryText",   0x00, 0xFF, 0x41);
                Brush(r, "Theme.SecondaryText", 0x00, 0xCC, 0x33);
                Brush(r, "Theme.DimText",       0x2A, 0x5C, 0x2A);
                Brush(r, "Theme.ScrollThumb",   0x1A, 0x4A, 0x1A);
                break;

            case AppSkin.Paper:
                ClearAcrylic();
                Brush(r, "Theme.SidebarBg",     0xFA, 0xF7, 0xF2);
                Brush(r, "Theme.HeaderBg",      0xF0, 0xEB, 0xE3);
                Brush(r, "Theme.PopupBg",       0xFF, 0xFD, 0xF9);
                Brush(r, "Theme.BorderBrush",   0xD4, 0xC5, 0xB0);
                Brush(r, "Theme.BorderSoft",    0xBF, 0xB0, 0x9A);
                Brush(r, "Theme.ItemHover",     0xEE, 0xE8, 0xDF);
                Brush(r, "Theme.ItemSelect",    0xE0, 0xD9, 0xCE);
                Brush(r, "Theme.PrimaryText",   0x2C, 0x24, 0x16);
                Brush(r, "Theme.SecondaryText", 0x4A, 0x3D, 0x2D);
                Brush(r, "Theme.DimText",       0x8C, 0x7B, 0x66);
                Brush(r, "Theme.ScrollThumb",   0xB0, 0xA0, 0x90);
                break;

            case AppSkin.Synthwave:
                ClearAcrylic();
                Brush(r, "Theme.SidebarBg",     0x0D, 0x07, 0x17);
                Brush(r, "Theme.HeaderBg",      0x13, 0x0B, 0x20);
                Brush(r, "Theme.PopupBg",       0x1A, 0x10, 0x28);
                Brush(r, "Theme.BorderBrush",   0xAA, 0x00, 0xFF);
                Brush(r, "Theme.BorderSoft",    0x2A, 0x10, 0x40);
                Brush(r, "Theme.ItemHover",     0x1A, 0x0A, 0x30);
                Brush(r, "Theme.ItemSelect",    0x25, 0x10, 0x40);
                Brush(r, "Theme.PrimaryText",   0xFF, 0x71, 0xCE);
                Brush(r, "Theme.SecondaryText", 0xB9, 0x67, 0xFF);
                Brush(r, "Theme.DimText",       0x4A, 0x20, 0x70);
                Brush(r, "Theme.ScrollThumb",   0x3A, 0x10, 0x60);
                break;

            case AppSkin.BrushedMetal:
                ClearAcrylic();
                Brush(r, "Theme.SidebarBg",     0x24, 0x26, 0x28);
                Brush(r, "Theme.HeaderBg",      0x2E, 0x30, 0x33);
                Brush(r, "Theme.PopupBg",       0x32, 0x34, 0x37);
                Brush(r, "Theme.BorderBrush",   0x5A, 0x5F, 0x65);
                Brush(r, "Theme.BorderSoft",    0x6A, 0x70, 0x75);
                Brush(r, "Theme.ItemHover",     0x2E, 0x32, 0x35);
                Brush(r, "Theme.ItemSelect",    0x38, 0x3D, 0x42);
                Brush(r, "Theme.PrimaryText",   0xD8, 0xDC, 0xE0);
                Brush(r, "Theme.SecondaryText", 0xB8, 0xBF, 0xC5);
                Brush(r, "Theme.DimText",       0x6A, 0x70, 0x78);
                Brush(r, "Theme.ScrollThumb",   0x5A, 0x60, 0x68);
                break;

            case AppSkin.HighContrast:
                ClearAcrylic();
                Brush(r, "Theme.SidebarBg",     0x00, 0x00, 0x00);
                Brush(r, "Theme.HeaderBg",      0x00, 0x00, 0x00);
                Brush(r, "Theme.PopupBg",       0x00, 0x00, 0x00);
                Brush(r, "Theme.BorderBrush",   0xFF, 0xFF, 0xFF);
                Brush(r, "Theme.BorderSoft",    0xFF, 0xFF, 0xFF);
                Brush(r, "Theme.ItemHover",     0x1A, 0x1A, 0x1A);
                Brush(r, "Theme.ItemSelect",    0x33, 0x99, 0xFF);
                Brush(r, "Theme.PrimaryText",   0xFF, 0xFF, 0xFF);
                Brush(r, "Theme.SecondaryText", 0xFF, 0xFF, 0xFF);
                Brush(r, "Theme.DimText",       0xFF, 0xFF, 0x00);
                Brush(r, "Theme.ScrollThumb",   0xFF, 0xFF, 0xFF);
                break;

            case AppSkin.Custom:
                ClearAcrylic();
                Apply(ThemeMode.Dark); // dark base — image overlays it
                break;
        }
    }

    // ── Font / icon scale ─────────────────────────────────────────────

    public static void ApplyFontScale(double scale)
    {
        var r = Application.Current.Resources;
        r["Theme.FontSize"]   = Math.Round(12.0 * scale, 1);
        r["Theme.IconWidth"]  = Math.Round(16.0 * scale, 1);
        r["Theme.IconHeight"] = Math.Round(13.0 * scale, 1);
    }

    // ── Acrylic (native) ──────────────────────────────────────────────

    private static void ApplyAcrylic(int gradientColor)
    {
        if (WindowHandle == nint.Zero) return;
        try
        {
            var accent = new NativeMethods.AccentPolicy
            {
                AccentState   = 4, // ACCENT_ENABLE_ACRYLICBLURBEHIND
                AccentFlags   = 0,
                GradientColor = gradientColor,
                AnimationId   = 0
            };
            int  sz  = Marshal.SizeOf(accent);
            nint ptr = Marshal.AllocHGlobal(sz);
            Marshal.StructureToPtr(accent, ptr, false);
            var data = new NativeMethods.WindowCompositionAttributeData
            {
                Attribute  = 19, // WCA_ACCENT_POLICY
                Data       = ptr,
                SizeOfData = sz
            };
            NativeMethods.SetWindowCompositionAttribute(WindowHandle, ref data);
            Marshal.FreeHGlobal(ptr);
        }
        catch { }
    }

    private static void ClearAcrylic()
    {
        if (WindowHandle == nint.Zero) return;
        try
        {
            var accent = new NativeMethods.AccentPolicy { AccentState = 0 }; // ACCENT_DISABLED
            int  sz  = Marshal.SizeOf(accent);
            nint ptr = Marshal.AllocHGlobal(sz);
            Marshal.StructureToPtr(accent, ptr, false);
            var data = new NativeMethods.WindowCompositionAttributeData
            {
                Attribute  = 19,
                Data       = ptr,
                SizeOfData = sz
            };
            NativeMethods.SetWindowCompositionAttribute(WindowHandle, ref data);
            Marshal.FreeHGlobal(ptr);
        }
        catch { }
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static void Brush(ResourceDictionary r, string key, byte red, byte grn, byte blu)
        => r[key] = new SolidColorBrush(Color.FromRgb(red, grn, blu));

    private static void BrushA(ResourceDictionary r, string key, byte alpha, byte red, byte grn, byte blu)
        => r[key] = new SolidColorBrush(Color.FromArgb(alpha, red, grn, blu));

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
