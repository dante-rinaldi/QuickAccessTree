using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SidebarBuddy.Models;

public class FolderNode : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private string _path = string.Empty;
    private string? _color;
    private bool _isExpanded;

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    public string Path
    {
        get => _path;
        set { _path = value; OnPropertyChanged(); }
    }

    // Hex color string e.g. "#FFC000". Null = default yellow.
    public string? Color
    {
        get => _color;
        set { _color = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasColor)); }
    }

    public bool HasColor => _color != null;

    public bool IsExpanded
    {
        get => _isExpanded;
        set { _isExpanded = value; OnPropertyChanged(); }
    }

    private bool _isMultiSelected;
    public bool IsMultiSelected
    {
        get => _isMultiSelected;
        set { _isMultiSelected = value; OnPropertyChanged(); }
    }

    // True once real children have been loaded (replaces the dummy placeholder)
    public bool ChildrenLoaded { get; set; }

    // Whether this node was manually added by the user (vs. imported from Quick Access)
    public bool IsCustom { get; set; }

    // True for group header nodes (no filesystem path — synthetic "§g§{guid}" key)
    public bool IsGroup { get; set; }

    // True for the single "loading…" placeholder child added before real children load
    public bool IsDummy { get; set; }

    // True for visual divider rows (synthetic "§d§{guid}" path, no filesystem backing)
    public bool IsDivider { get; set; }

    public ObservableCollection<FolderNode> Children { get; set; } = new();

    public static FolderNode MakeDummy() => new() { Name = "Loading…", IsDummy = true };

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
