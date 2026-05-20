using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
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

    private readonly Dictionary<string, FolderNode> _nodesByPath =
        new(StringComparer.OrdinalIgnoreCase);

    private void LoadTree()
    {
        var available = new Dictionary<string, FolderNode>(StringComparer.OrdinalIgnoreCase);

        if (_settings.ImportQuickAccess)
        {
            foreach (var n in _qaSvc.GetPinnedFolders())
            {
                if (_settings.RemovedPaths.Contains(n.Path, StringComparer.OrdinalIgnoreCase))
                    continue;
                if (available.ContainsKey(n.Path)) continue;
                ApplyColor(n);
                available[n.Path] = n;
            }
        }

        foreach (var cf in _settings.CustomFolders)
        {
            if (!Directory.Exists(cf.Path)) continue;
            if (available.ContainsKey(cf.Path)) continue;
            var n = new FolderNode { Name = cf.DisplayName, Path = cf.Path, IsCustom = true };
            ApplyColor(n);
            available[cf.Path] = n;
        }

        bool placementsChanged = SyncPlacements(available);

        // Unhook handlers from old nodes before we replace the registry
        foreach (var old in _nodesByPath.Values)
            old.PropertyChanged -= Node_PropertyChanged;
        _nodesByPath.Clear();
        foreach (var kv in available) _nodesByPath[kv.Key] = kv.Value;

        // Build hierarchy by walking Placements in order
        foreach (var node in available.Values) node.Children.Clear();
        var roots = new ObservableCollection<FolderNode>();
        foreach (var p in _settings.Placements)
        {
            if (!available.TryGetValue(p.Path, out var node)) continue;
            if (p.ParentPath != null && available.TryGetValue(p.ParentPath, out var parent))
                parent.Children.Add(node);
            else
                roots.Add(node);
        }

        // Apply expanded state and hook persistence
        foreach (var node in available.Values)
        {
            if (_settings.ExpandedPaths.TryGetValue(node.Path, out var ex))
                node.IsExpanded = ex;
            node.PropertyChanged += Node_PropertyChanged;
        }

        FolderTree.ItemsSource = roots;

        if (placementsChanged) _settingsSvc.Save(_settings);
    }

    private void ApplyColor(FolderNode n)
    {
        if (_settings.FolderColors.TryGetValue(n.Path, out var c))
            n.Color = c;
    }

    private void Node_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(FolderNode.IsExpanded)) return;
        if (sender is not FolderNode n) return;
        _settings.ExpandedPaths[n.Path] = n.IsExpanded;
        _settingsSvc.Save(_settings);
    }

    /// <summary>
    /// Drops placements for paths that no longer exist, appends new folders with
    /// parents inferred from path ancestry, and rebinds orphaned children to the
    /// nearest still-existing ancestor.
    /// </summary>
    private bool SyncPlacements(Dictionary<string, FolderNode> available)
    {
        bool changed = false;

        int removed = _settings.Placements.RemoveAll(p => !available.ContainsKey(p.Path));
        if (removed > 0) changed = true;

        var placed = new HashSet<string>(
            _settings.Placements.Select(p => p.Path),
            StringComparer.OrdinalIgnoreCase);

        // Append unplaced folders. Sort shortest-first so parents land in
        // `placed` before their would-be children look for an ancestor.
        foreach (var path in available.Keys
                     .Where(k => !placed.Contains(k))
                     .OrderBy(k => k.Length))
        {
            string? parent = FindAncestorIn(path, placed);
            _settings.Placements.Add(new FolderPlacement { Path = path, ParentPath = parent });
            placed.Add(path);
            changed = true;
        }

        // Reparent any placement whose parent is gone
        foreach (var p in _settings.Placements)
        {
            if (p.ParentPath == null) continue;
            if (placed.Contains(p.ParentPath)) continue;
            p.ParentPath = FindAncestorIn(p.Path, placed, excluding: p.Path);
            changed = true;
        }

        return changed;
    }

    private static string? FindAncestorIn(
        string path, IEnumerable<string> candidates, string? excluding = null)
    {
        string? best = null;
        foreach (var c in candidates)
        {
            if (excluding != null && c.Equals(excluding, StringComparison.OrdinalIgnoreCase))
                continue;
            var trimmed = c.TrimEnd('\\', '/');
            if (path.StartsWith(trimmed + "\\", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(trimmed + "/",  StringComparison.OrdinalIgnoreCase))
            {
                if (best == null || c.Length > best.Length) best = c;
            }
        }
        return best;
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

    // ── Drag and drop reorder/reparent ────────────────────────────────────

    private Point          _dragStart;
    private FolderNode?    _dragCandidate;
    private DropAdorner?   _dropAdorner;
    private Border?        _dropAdornerTarget;

    private void FolderTree_PreviewLeftDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(null);
        var tvi = (e.OriginalSource as DependencyObject)?.FindAncestorOrSelf<TreeViewItem>();
        _dragCandidate = tvi?.DataContext as FolderNode;
    }

    private void FolderTree_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragCandidate == null) return;
        var pos = e.GetPosition(null);
        if (Math.Abs(pos.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(pos.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        var node = _dragCandidate;
        _dragCandidate = null;
        DragDrop.DoDragDrop(FolderTree, node, DragDropEffects.Move);
        RemoveDropAdorner();
    }

    private void FolderTree_DragOver(object sender, DragEventArgs e)
    {
        if (TryResolveDropTarget(e, out var tvi, out var zone) &&
            tvi!.DataContext is FolderNode tNode &&
            e.Data.GetData(typeof(FolderNode)) is FolderNode sNode &&
            IsValidDrop(sNode, tNode))
        {
            e.Effects = DragDropEffects.Move;
            ShowDropAdorner(tvi, zone);
        }
        else
        {
            e.Effects = DragDropEffects.None;
            RemoveDropAdorner();
        }
        e.Handled = true;
    }

    private void FolderTree_DragLeave(object sender, DragEventArgs e) => RemoveDropAdorner();

    private void FolderTree_Drop(object sender, DragEventArgs e)
    {
        RemoveDropAdorner();
        if (!TryResolveDropTarget(e, out var tvi, out var zone)) return;
        if (tvi!.DataContext is not FolderNode tNode) return;
        if (e.Data.GetData(typeof(FolderNode)) is not FolderNode sNode) return;
        if (!IsValidDrop(sNode, tNode)) return;

        ApplyDrop(sNode, tNode, zone);
        _settingsSvc.Save(_settings);
        LoadTree();
        e.Handled = true;
    }

    private bool TryResolveDropTarget(DragEventArgs e, out TreeViewItem? tvi, out DropZone zone)
    {
        zone = DropZone.None;
        tvi = (e.OriginalSource as DependencyObject)?.FindAncestorOrSelf<TreeViewItem>();
        if (tvi == null) return false;
        if (tvi.Template.FindName("Row", tvi) is not Border row) return false;

        var rowPos = e.GetPosition(row);
        double h = row.ActualHeight;
        if (h <= 0) return false;

        double ratio = rowPos.Y / h;
        zone = ratio < 0.3 ? DropZone.Before
             : ratio < 0.7 ? DropZone.Into
             : DropZone.After;
        return true;
    }

    private bool IsValidDrop(FolderNode source, FolderNode target)
    {
        if (ReferenceEquals(source, target)) return false;
        if (source.Path.Equals(target.Path, StringComparison.OrdinalIgnoreCase)) return false;
        // Walk target's parent chain — if we hit source, dropping would create a cycle
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? current = target.Path;
        while (current != null && seen.Add(current))
        {
            var p = _settings.Placements.FirstOrDefault(
                x => x.Path.Equals(current, StringComparison.OrdinalIgnoreCase));
            if (p?.ParentPath == null) return true;
            if (p.ParentPath.Equals(source.Path, StringComparison.OrdinalIgnoreCase)) return false;
            current = p.ParentPath;
        }
        return true;
    }

    private void ApplyDrop(FolderNode source, FolderNode target, DropZone zone)
    {
        var list = _settings.Placements;
        int srcIdx = list.FindIndex(p => p.Path.Equals(source.Path, StringComparison.OrdinalIgnoreCase));
        int tgtIdx = list.FindIndex(p => p.Path.Equals(target.Path, StringComparison.OrdinalIgnoreCase));
        if (srcIdx < 0 || tgtIdx < 0) return;

        var sp = list[srcIdx];
        list.RemoveAt(srcIdx);
        if (srcIdx < tgtIdx) tgtIdx--;

        int insertAt;
        string? newParent;
        switch (zone)
        {
            case DropZone.Before:
                insertAt = tgtIdx;
                newParent = list[tgtIdx].ParentPath;
                break;
            case DropZone.After:
                insertAt = tgtIdx + 1;
                newParent = list[tgtIdx].ParentPath;
                break;
            case DropZone.Into:
                newParent = target.Path;
                // Insert as last direct child of target
                insertAt = tgtIdx + 1;
                for (int i = tgtIdx + 1; i < list.Count; i++)
                {
                    if (list[i].ParentPath != null &&
                        list[i].ParentPath!.Equals(target.Path, StringComparison.OrdinalIgnoreCase))
                        insertAt = i + 1;
                }
                break;
            default:
                list.Insert(srcIdx, sp);
                return;
        }

        sp.ParentPath = newParent;
        list.Insert(insertAt, sp);
    }

    private void ShowDropAdorner(TreeViewItem tvi, DropZone zone)
    {
        if (tvi.Template.FindName("Row", tvi) is not Border row) return;
        if (!ReferenceEquals(_dropAdornerTarget, row))
        {
            RemoveDropAdorner();
            var layer = AdornerLayer.GetAdornerLayer(row);
            if (layer == null) return;
            _dropAdorner = new DropAdorner(row);
            layer.Add(_dropAdorner);
            _dropAdornerTarget = row;
        }
        _dropAdorner!.Zone = zone;
        _dropAdorner.InvalidateVisual();
    }

    private void RemoveDropAdorner()
    {
        if (_dropAdorner != null && _dropAdornerTarget != null)
        {
            var layer = AdornerLayer.GetAdornerLayer(_dropAdornerTarget);
            layer?.Remove(_dropAdorner);
        }
        _dropAdorner = null;
        _dropAdornerTarget = null;
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

internal enum DropZone { None, Before, Into, After }

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

// ── Drop-zone visual ──────────────────────────────────────────────────────

internal class DropAdorner : Adorner
{
    private static readonly Pen   LinePen  = MakeFrozen(new Pen(Brushes.DodgerBlue, 2));
    private static readonly Brush IntoFill = MakeFrozen(new SolidColorBrush(Color.FromArgb(80, 30, 144, 255)));

    public DropZone Zone { get; set; }

    public DropAdorner(UIElement adornedElement) : base(adornedElement)
    {
        IsHitTestVisible = false;
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = AdornedElement.RenderSize.Width;
        double h = AdornedElement.RenderSize.Height;
        switch (Zone)
        {
            case DropZone.Before:
                dc.DrawLine(LinePen, new Point(0, 0), new Point(w, 0));
                break;
            case DropZone.After:
                dc.DrawLine(LinePen, new Point(0, h), new Point(w, h));
                break;
            case DropZone.Into:
                dc.DrawRectangle(IntoFill, null, new Rect(0, 0, w, h));
                break;
        }
    }

    private static T MakeFrozen<T>(T f) where T : Freezable { f.Freeze(); return f; }
}
