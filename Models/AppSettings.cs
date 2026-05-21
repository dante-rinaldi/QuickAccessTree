namespace SidebarBuddy.Models;

public enum DockSide             { Right, Left }
public enum AddFolderMode        { CurrentFolder, BrowseDialog }
public enum ColorInheritanceMode { PerFolder, Cascade }
public enum ShowDelay            { Instant, HalfSecond, TwoSeconds, FiveSeconds }
public enum ThemeMode            { System, Dark, Light }
public enum AppSkin
{
    SolidDark, SolidLight, FrostedGlass, Mica,
    NeonCyber, Terminal, Paper, Synthwave, BrushedMetal, HighContrast
}

public class AppSettings
{
    public List<CustomFolder>    CustomFolders     { get; set; } = new();
    public Dictionary<string, string> FolderColors { get; set; } = new();
    public bool   ImportQuickAccess  { get; set; } = true;
    public double SidebarWidthDip    { get; set; } = 220;
    public DockSide DockSide         { get; set; } = DockSide.Right;
    public List<string> RemovedPaths { get; set; } = new();
    public List<FolderPlacement> Placements  { get; set; } = new();
    public Dictionary<string, bool> ExpandedPaths { get; set; } = new();

    // General settings
    public ColorInheritanceMode ColorInheritance   { get; set; } = ColorInheritanceMode.PerFolder;
    public bool                 AutoHide            { get; set; } = false;
    public ShowDelay            VisibilityDelay     { get; set; } = ShowDelay.Instant;
    public bool                 LaunchOnStartup     { get; set; } = false;
    public bool                 RestoreExpandedState{ get; set; } = true;
    public AddFolderMode        AddFolderBehavior   { get; set; } = AddFolderMode.CurrentFolder;
    public bool                 AutoNestFolders     { get; set; } = true;

    // Appearance
    public ThemeMode Theme { get; set; } = ThemeMode.System;
    public AppSkin   Skin  { get; set; } = AppSkin.SolidDark;

    // License / trial
    public DateTime TrialStartDate { get; set; } = DateTime.UtcNow;
    public bool     IsRegistered   { get; set; } = false;
    public string?  LicenseKey     { get; set; }
}

public class CustomFolder
{
    public string Path        { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

public class FolderPlacement
{
    public string  Path       { get; set; } = string.Empty;
    public string? ParentPath { get; set; }
}
