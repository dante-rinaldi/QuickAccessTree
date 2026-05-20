namespace QuickAccessTree.Models;

public enum DockSide { Right, Left }

public class AppSettings
{
    public List<CustomFolder> CustomFolders { get; set; } = new();

    // path → hex color, applies to both QA-imported and custom folders
    public Dictionary<string, string> FolderColors { get; set; } = new();

    public bool ImportQuickAccess { get; set; } = true;
    public double SidebarWidthDip { get; set; } = 220;
    public DockSide DockSide { get; set; } = DockSide.Right;

    // Paths explicitly removed by the user (suppressed even if in Quick Access)
    public List<string> RemovedPaths { get; set; } = new();
}

public class CustomFolder
{
    public string Path { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}
