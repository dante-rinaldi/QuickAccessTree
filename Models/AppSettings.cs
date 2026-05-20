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

    // Ordered list defining hierarchy and sibling order.
    // List order = sibling order within each parent. Folders not in this list
    // are appended on first appearance with parent inferred from path ancestry.
    public List<FolderPlacement> Placements { get; set; } = new();

    // Persisted expansion state — path → IsExpanded
    public Dictionary<string, bool> ExpandedPaths { get; set; } = new();
}

public class CustomFolder
{
    public string Path { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

public class FolderPlacement
{
    public string Path { get; set; } = string.Empty;
    // null = root-level node
    public string? ParentPath { get; set; }
}
