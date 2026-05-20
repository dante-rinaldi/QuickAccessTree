using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using QuickAccessTree.Models;
using QuickAccessTree.Services;

namespace QuickAccessTree;

public partial class MainWindow : Window
{
    // ── State ─────────────────────────────────────────────────────────────

    private AppSettings             _settings    = new();
    private SettingsService         _settingsSvc = new();
    private QuickAccessService      _qaSvc       = new();
    private ExplorerAttachService?  _attachSvc;

    private FolderNode? _contextNode;   // node targeted by right-click
    private bool        _isCollapsed;
    private const double ExpandedWidth  = 220;
    private const double CollapsedWidth = 24;

    private static readonly string[] PresetColors =
    {
        "#FFC000", "#FF5252", "#69F0AE", "#40C4FF",
        "#FF9100", "#E040FB", "#00E5FF", "#FF4081",
        "#BDBDBD", "#90A4AE", "#BCAAA4", "#FFFFFF",
    };

    // ── Init ──────────────────────────────────────────────────────────────

    public MainWindow()
    {
        InitializeComponent();
        BuildColorSwatches();
    }

    public void Initialize(
        AppSettings settings,
        ExplorerAttachService attachSvc,
        SettingsService settingsSvc)
    {
        _settings    = settings;
        _attachSvc   = attachSvc;
        _settingsSvc = settingsSvc;
        LoadTree();
        ApplyDockCorners();
    }

    // ── Tree loading ──────────────────────────────────────────────────────

    private void LoadTree()
    {
        var flat = new List<FolderNode>();

        if (_settings.ImportQuickAccess)
        {
            foreach (var n in _qaSvc.GetPinnedFolders())
            {
                if (_settings.RemovedPaths.Contains(n.Path, StringComparer.OrdinalIgnoreCase))
                    continue;
                ApplyColor(n);
                flat.Add(n);
            }
        }

        foreach (var cf in _settings.CustomFolders)
        {
            if (!Directory.Exists(cf.Path)) continue;
            if (flat.Any(f => f.Path.Equals(cf.Path, StringComparison.OrdinalIgnoreCase))) continue;
            var n = new FolderNode { Name = cf.DisplayName, Path = cf.Path, IsCustom = true };
            ApplyColor(n);
            flat.Add(n);
        }

        var roots = BuildHierarchy(flat);
        FolderTree.ItemsSource = new ObservableCollection<FolderNode>(roots);
    }

    private void ApplyColor(FolderNode n)
    {
        if (_settings.FolderColors.TryGetValue(n.Path, out var c))
            n.Color = c;
    }

    /// <summary>
    /// Organises a flat list of folders into a tree using path ancestry.
    /// Only folders that are already in the list become children — we never
    /// auto-expand the filesystem.
    /// </summary>
    private static List<FolderNode> BuildHierarchy(List<FolderNode> flat)
    {
        // Work on a copy sorted shortest-path-first so parents are placed before children
        var sorted = flat.OrderBy(f => f.Path.Length).ThenBy(f => f.Path).ToList();
        var placed = new List<FolderNode>();
        var roots  = new List<FolderNode>();

        foreach (var node in sorted)
        {
            // Find the deepest already-placed folder that is a direct ancestor
            FolderNode? best = null;
            foreach (var candidate in placed)
            {
                string cp = candidate.Path.TrimEnd('\\', '/');
                if (node.Path.StartsWith(cp + "\\", StringComparison.OrdinalIgnoreCase) ||
                    node.Path.StartsWith(cp + "/",  StringComparison.OrdinalIgnoreCase))
                {
                    if (best == null || candidate.Path.Length > best.Path.Length)
                        best = candidate;
                }
            }

            if (best != null)
                best.Children.Add(node);
            else
                roots.Add(node);

            placed.Add(node);
        }

        return roots;
    }

    // ── Folder selection → navigate Explorer ──────────────────────────────

    private void FolderTree_SelectedItemChanged(object sender,
        RoutedPropertyChangedEventArgs<object> e)
    {
        bool hasSelection = e.NewValue is FolderNode;
        RemoveBtn.IsEnabled = hasSelection;

        if (e.NewValue is FolderNode node)
            _attachSvc?.NavigateTo(node.Path);
    }

    // ── Header buttons ────────────────────────────────────────────────────

    private void RefreshBtn_Click(object sender, RoutedEventArgs e) => LoadTree();

    private void DockToggleBtn_Click(object sender, RoutedEventArgs e)
    {
        _settings.DockSide = _settings.DockSide == DockSide.Right
            ? DockSide.Left : DockSide.Right;
        _settingsSvc.Save(_settings);
        _attachSvc?.UpdateDockSide(_settings.DockSide);
        ApplyDockCorners();
    }

    private void RemoveBtn_Click(object sender, RoutedEventArgs e)
    {
        if (FolderTree.SelectedItem is FolderNode node)
            RemoveFolderNode(node);
    }

    private void CollapseBtn_Click(object sender, RoutedEventArgs e)
    {
        _isCollapsed = !_isCollapsed;

        if (_isCollapsed)
        {
            OuterBorder.Visibility  = Visibility.Collapsed;
            CollapsedTab.Visibility = Visibility.Visible;
            // Explicit detach restores Explorer and hides sidebar
            _attachSvc?.ExplicitDetach();
        }
        else
        {
            CollapsedTab.Visibility = Visibility.Collapsed;
            OuterBorder.Visibility  = Visibility.Visible;
            // Re-attach to whatever Explorer is open
            _attachSvc?.ForceAttach();
        }

        // Update collapse button arrow direction
        CollapseBtn.Content = _isCollapsed ? "▶" : "◀";
    }

    // ── Dock corner styling ───────────────────────────────────────────────

    private void ApplyDockCorners()
    {
        bool right = _settings.DockSide == DockSide.Right;

        OuterBorder.CornerRadius = right
            ? new CornerRadius(6, 0, 0, 6)
            : new CornerRadius(0, 6, 6, 0);

        HeaderBorder.CornerRadius = right
            ? new CornerRadius(6, 0, 0, 0)
            : new CornerRadius(0, 6, 0, 0);

        FooterBorder.CornerRadius = right
            ? new CornerRadius(0, 0, 0, 6)
            : new CornerRadius(0, 0, 6, 0);

        CollapsedTab.CornerRadius = right
            ? new CornerRadius(6, 0, 0, 6)
            : new CornerRadius(0, 6, 6, 0);

        OuterBorder.BorderThickness = right
            ? new Thickness(1, 1, 0, 1)
            : new Thickness(0, 1, 1, 1);

        CollapsedTab.BorderThickness = right
            ? new Thickness(1, 1, 0, 1)
            : new Thickness(0, 1, 1, 1);

        // Collapse/expand arrows flip with dock side
        // Right-docked: ◀ collapses left,  ▶ expands right
        // Left-docked:  ▶ collapses right, ◀ expands left
        CollapseBtn.Content = _isCollapsed
            ? (right ? "▶" : "◀")
            : (right ? "◀" : "▶");
    }

    // ── Add folder ────────────────────────────────────────────────────────

    private void AddFolderBtn_Click(object sender, RoutedEventArgs e) => AddFolder();
    private void CtxAdd_Click(object sender, RoutedEventArgs e)        => AddFolder();

    private void AddFolder()
    {
        using var dlg = new System.Windows.Forms.FolderBrowserDialog
        {
            Description           = "Select a folder to add",
            UseDescriptionForTitle = true,
        };
        if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

        string path = dlg.SelectedPath;
        if (!Directory.Exists(path)) return;

        if (FolderTree.ItemsSource is ObservableCollection<FolderNode> roots &&
            roots.Any(r => r.Path.Equals(path, StringComparison.OrdinalIgnoreCase)))
            return;

        string name = System.IO.Path.GetFileName(path);
        var node = new FolderNode { Name = name, Path = path, IsCustom = true };

        // Re-load tree so the hierarchy is recalculated
        _settings.CustomFolders.Add(new CustomFolder { Path = path, DisplayName = name });
        _settingsSvc.Save(_settings);
        LoadTree();
    }

    // ── Context menu ──────────────────────────────────────────────────────

    private void FolderTree_PreviewRightClick(object sender, MouseButtonEventArgs e)
    {
        var item = (e.OriginalSource as DependencyObject)
            ?.FindAncestorOrSelf<TreeViewItem>();

        _contextNode = item?.DataContext as FolderNode;
        CtxRemoveItem.IsEnabled = _contextNode != null;
    }

    private void CtxOpen_Click(object sender, RoutedEventArgs e)
    {
        if (_contextNode is { } n)
            System.Diagnostics.Process.Start("explorer.exe", n.Path);
    }

    private void CtxRemove_Click(object sender, RoutedEventArgs e)
    {
        if (_contextNode is { } n)
            RemoveFolderNode(n);
    }

    private void RemoveFolderNode(FolderNode node)
    {
        // Remove from settings
        _settings.CustomFolders.RemoveAll(
            cf => cf.Path.Equals(node.Path, StringComparison.OrdinalIgnoreCase));
        // Also remove from QA-imported set by marking as "don't show"
        // (we track removed QA folders via a blocklist)
        if (!_settings.RemovedPaths.Contains(node.Path))
            _settings.RemovedPaths.Add(node.Path);

        _settingsSvc.Save(_settings);
        LoadTree();
    }

    // ── Color picker ──────────────────────────────────────────────────────

    private void BuildColorSwatches()
    {
        foreach (var hex in PresetColors)
        {
            var swatch = new Border
            {
                Width        = 22,
                Height       = 22,
                Margin       = new Thickness(3),
                CornerRadius = new CornerRadius(4),
                Background   = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString(hex)),
                Cursor  = Cursors.Hand,
                Tag     = hex,
                ToolTip = hex,
            };
            swatch.MouseLeftButtonUp += Swatch_Click;
            ColorSwatchPanel.Children.Add(swatch);
        }
    }

    private void CtxColor_Click(object sender, RoutedEventArgs e)
    {
        if (_contextNode != null)
            ColorPickerPopup.IsOpen = true;
    }

    private void Swatch_Click(object sender, MouseButtonEventArgs e)
    {
        if (_contextNode is not { } n) return;
        if (sender is Border { Tag: string hex })
        {
            n.Color = hex;
            _settings.FolderColors[n.Path] = hex;
            _settingsSvc.Save(_settings);
        }
        ColorPickerPopup.IsOpen = false;
    }

    private void ColorReset_Click(object sender, RoutedEventArgs e)
    {
        if (_contextNode is not { } n) return;
        n.Color = null;
        _settings.FolderColors.Remove(n.Path);
        _settingsSvc.Save(_settings);
        ColorPickerPopup.IsOpen = false;
    }

    // ── Rename ────────────────────────────────────────────────────────────

    private void CtxRename_Click(object sender, RoutedEventArgs e)
    {
        if (_contextNode is not { } n) return;
        RenameBox.Text = n.Name;
        RenameBox.SelectAll();
        RenamePopup.IsOpen = true;
        RenameBox.Focus();
    }

    private void RenameBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)  CommitRename();
        if (e.Key == Key.Escape) RenamePopup.IsOpen = false;
    }

    private void RenameOk_Click(object sender, RoutedEventArgs e)     => CommitRename();
    private void RenameCancel_Click(object sender, RoutedEventArgs e) => RenamePopup.IsOpen = false;

    private void CommitRename()
    {
        if (_contextNode is not { } n) return;
        string newName = RenameBox.Text.Trim();
        if (string.IsNullOrEmpty(newName)) return;

        n.Name = newName;
        var cf = _settings.CustomFolders
            .FirstOrDefault(f => f.Path.Equals(n.Path, StringComparison.OrdinalIgnoreCase));
        if (cf != null) cf.DisplayName = newName;

        _settingsSvc.Save(_settings);
        RenamePopup.IsOpen = false;
    }
}

// ── Visual tree extension ─────────────────────────────────────────────────

internal static class VisualExtensions
{
    public static T? FindAncestorOrSelf<T>(this DependencyObject obj) where T : DependencyObject
    {
        while (obj != null)
        {
            if (obj is T t) return t;
            obj = VisualTreeHelper.GetParent(obj) ?? LogicalTreeHelper.GetParent(obj);
        }
        return null;
    }
}
