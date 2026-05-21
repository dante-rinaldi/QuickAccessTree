using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using SidebarBuddy.Interop;
using SidebarBuddy.Models;
using SidebarBuddy.Services;

namespace SidebarBuddy;

public partial class MainWindow : Window
{
    // ── State ─────────────────────────────────────────────────────────────

    private AppSettings             _settings    = new();
    private SettingsService         _settingsSvc = new();
    private QuickAccessService      _qaSvc       = new();
    private ExplorerAttachService?  _attachSvc;

    private FolderNode? _contextNode;   // node targeted by right-click
    private bool        _isCollapsed;
    private bool        _suppressNavigation;
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
        attachSvc.AutoHide = settings.AutoHide;
        LoadTree();
        ApplyDockCorners();
    }

    // ── Tree loading ──────────────────────────────────────────────────────

    private readonly Dictionary<string, FolderNode> _nodesByPath =
        new(StringComparer.OrdinalIgnoreCase);

    private void LoadTree()
    {
        _suppressNavigation = true;
        try { LoadTreeCore(); }
        finally { _suppressNavigation = false; }
    }

    private void LoadTreeCore()
    {
        var available = new Dictionary<string, FolderNode>(StringComparer.OrdinalIgnoreCase);

        // Groups first — synthetic paths ("§g§{guid}") are never path-ancestors of real folders
        foreach (var (synth, name) in _settings.GroupNames)
            available[synth] = new FolderNode { Name = name, Path = synth, IsGroup = true };

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

        // Apply color cascade if enabled (path-based, works regardless of tree position)
        if (_settings.ColorInheritance == ColorInheritanceMode.Cascade)
            ApplyCascadeColors(available.Values);

        // Apply expanded state and hook persistence
        foreach (var node in available.Values)
        {
            if (_settings.RestoreExpandedState &&
                _settings.ExpandedPaths.TryGetValue(node.Path, out var ex))
                node.IsExpanded = ex;
            node.PropertyChanged += Node_PropertyChanged;
        }

        FolderTree.ItemsSource = roots;

        if (placementsChanged) _settingsSvc.Save(_settings);

        ApplyQuickLinks();
    }  // end LoadTreeCore

    private void ApplyColor(FolderNode n)
    {
        if (_settings.FolderColors.TryGetValue(n.Path, out var c))
            n.Color = c;
    }

    private void ApplyCascadeColors(IEnumerable<FolderNode> nodes)
    {
        // Path-based: for each folder without an explicit color, find the longest
        // ancestor path in FolderColors. Works regardless of tree position.
        foreach (var node in nodes)
        {
            if (node.IsGroup) continue;
            if (_settings.FolderColors.ContainsKey(node.Path)) continue;

            string? bestAncestor = null;
            foreach (var coloredPath in _settings.FolderColors.Keys)
            {
                var trimmed = coloredPath.TrimEnd('\\', '/');
                if (!node.Path.StartsWith(trimmed + "\\", StringComparison.OrdinalIgnoreCase) &&
                    !node.Path.StartsWith(trimmed + "/",  StringComparison.OrdinalIgnoreCase))
                    continue;
                if (bestAncestor == null || coloredPath.Length > bestAncestor.Length)
                    bestAncestor = coloredPath;
            }
            if (bestAncestor != null)
                node.Color = _settings.FolderColors[bestAncestor];
        }
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

        // Append unplaced items at root — user can drag to reorder/nest
        foreach (var path in available.Keys
                     .Where(k => !placed.Contains(k))
                     .OrderBy(k => k.Length))
        {
            _settings.Placements.Add(new FolderPlacement { Path = path, ParentPath = null });
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

        // Don't navigate during tree reload, and skip group headers (no real path)
        if (_suppressNavigation) return;
        if (e.NewValue is FolderNode node && !node.IsGroup)
            _attachSvc?.NavigateTo(node.Path);
    }

    // ── Header buttons ────────────────────────────────────────────────────

    public void ReloadTree() => LoadTree();

    private void SettingsBtn_Click(object sender, RoutedEventArgs e)
        => ((App)Application.Current).OpenSettings();

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

    private void CollapsedTab_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && _isCollapsed)
            CollapseBtn_Click(sender, e);
    }

    private void CollapseBtn_Click(object sender, RoutedEventArgs e)
    {
        _isCollapsed = !_isCollapsed;

        if (_isCollapsed)
        {
            OuterBorder.Visibility  = Visibility.Collapsed;
            CollapsedTab.Visibility = Visibility.Visible;
            if (_attachSvc != null)
            {
                _attachSvc.IsCollapsed = true;
                _attachSvc.RefreshPosition();
            }
        }
        else
        {
            CollapsedTab.Visibility = Visibility.Collapsed;
            OuterBorder.Visibility  = Visibility.Visible;
            if (_attachSvc != null)
            {
                _attachSvc.IsCollapsed = false;
                _attachSvc.RefreshPosition();
            }
            _attachSvc?.ForceAttach();
        }

        ApplyDockCorners();
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
        CollapseBtn.Content = _isCollapsed
            ? (right ? "▶" : "◀")
            : (right ? "◀" : "▶");
        ExpandBtn.Content = right ? "▶" : "◀";

        // Resize grip sits on the open (away-from-Explorer) edge
        ResizeGrip.HorizontalAlignment = right
            ? HorizontalAlignment.Right
            : HorizontalAlignment.Left;
    }

    // ── Add group ─────────────────────────────────────────────────────────

    private void AddGroupBtn_Click(object sender, RoutedEventArgs e)
    {
        GroupNameBox.Text = string.Empty;
        GroupNamePopup.IsOpen = true;
        GroupNameBox.Focus();
    }

    private void GroupNameBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)  CommitGroupCreate();
        if (e.Key == Key.Escape) GroupNamePopup.IsOpen = false;
    }

    private void GroupOk_Click(object sender, RoutedEventArgs e)     => CommitGroupCreate();
    private void GroupCancel_Click(object sender, RoutedEventArgs e) => GroupNamePopup.IsOpen = false;

    // Remove WS_EX_NOACTIVATE from the popup's own HWND so its TextBox accepts keyboard input.
    // WPF popups inherit NOACTIVATE from the parent window; we strip it per-popup on open.
    private void GroupNamePopup_Opened(object sender, EventArgs e)
        => ActivatePopupTextBox(GroupNameBox);

    private void RenamePopup_Opened(object sender, EventArgs e)
        => ActivatePopupTextBox(RenameBox);

    private void ActivatePopupTextBox(TextBox box)
    {
        if (HwndSource.FromVisual(box) is not HwndSource src) return;
        int ex = NativeMethods.GetWindowLong(src.Handle, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLong(src.Handle, NativeMethods.GWL_EXSTYLE,
            ex & ~NativeMethods.WS_EX_NOACTIVATE);
        NativeMethods.SetForegroundWindow(src.Handle);
        box.Focus();
        box.SelectAll();
    }

    private void CommitGroupCreate()
    {
        string name = GroupNameBox.Text.Trim();
        if (string.IsNullOrEmpty(name)) return;

        string synth = "§g§" + Guid.NewGuid().ToString("N");
        _settings.GroupNames[synth] = name;
        _settingsSvc.Save(_settings);
        GroupNamePopup.IsOpen = false;
        LoadTree();
    }

    // ── Add folder ────────────────────────────────────────────────────────

    private void AddFolderBtn_Click(object sender, RoutedEventArgs e) => AddFolder();
    private void CtxAdd_Click(object sender, RoutedEventArgs e)        => AddFolder();

    private void AddFolder()
    {
        string? path;

        if (_settings.AddFolderBehavior == AddFolderMode.CurrentFolder)
        {
            path = _attachSvc?.GetCurrentExplorerPath();
            if (string.IsNullOrEmpty(path))
            {
                path = BrowseForFolder();
                if (path == null) return;
            }
        }
        else if (_settings.AddFolderBehavior == AddFolderMode.SelectedItem)
        {
            // First try the selected item in Explorer's file pane; fall back to current folder
            path = _attachSvc?.GetSelectedExplorerItem();
            if (string.IsNullOrEmpty(path))
                path = _attachSvc?.GetCurrentExplorerPath();
            if (string.IsNullOrEmpty(path))
            {
                path = BrowseForFolder();
                if (path == null) return;
            }
        }
        else
        {
            path = BrowseForFolder();
            if (path == null) return;
        }

        if (!Directory.Exists(path)) return;

        if (_settings.CustomFolders.Any(
                f => f.Path.Equals(path, StringComparison.OrdinalIgnoreCase)))
            return;

        // Path.GetFileName returns "" for root drives (e.g. "C:\"); fall back to the trimmed path
        string name = System.IO.Path.GetFileName(path.TrimEnd('\\', '/'));
        if (string.IsNullOrEmpty(name)) name = path.TrimEnd('\\', '/');
        _settings.CustomFolders.Add(new CustomFolder { Path = path, DisplayName = name });
        _settingsSvc.Save(_settings);
        LoadTree();
    }

    private static string? BrowseForFolder()
    {
        using var dlg = new System.Windows.Forms.FolderBrowserDialog
        {
            Description            = "Select a folder to add",
            UseDescriptionForTitle = true,
        };
        return dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK
            ? dlg.SelectedPath : null;
    }

    // ── Click-to-focus Explorer ───────────────────────────────────────────

    private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        // Don't steal focus when the user is typing in a popup
        if (ColorPickerPopup.IsOpen || RenamePopup.IsOpen || GroupNamePopup.IsOpen) return;
        _attachSvc?.FocusExplorer();
    }

    // ── Sidebar width resize ──────────────────────────────────────────────

    private const double MinSidebarWidth = 140;
    private const double MaxSidebarWidth = 600;

    private bool   _resizing;
    private double _resizeStartX;
    private double _resizeStartWidth;
    private double _resizeStartLeft;

    private void ResizeGrip_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        _resizing        = true;
        _resizeStartX     = PointToScreen(e.GetPosition(this)).X;
        _resizeStartWidth = Width;
        _resizeStartLeft  = Left;
        ResizeGrip.CaptureMouse();
        e.Handled = true;
    }

    private void ResizeGrip_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_resizing || e.LeftButton != MouseButtonState.Pressed) return;
        double delta = PointToScreen(e.GetPosition(this)).X - _resizeStartX;

        double newWidth;
        if (_settings.DockSide == DockSide.Right)
        {
            // Sidebar is to the right of Explorer; open edge is the right side
            // Dragging right = wider, dragging left = narrower
            newWidth = Math.Clamp(_resizeStartWidth + delta, MinSidebarWidth, MaxSidebarWidth);
            Width = newWidth;
        }
        else
        {
            // Sidebar is to the left of Explorer; open edge is the left side
            // Dragging left = wider (width grows, left position moves left)
            newWidth = Math.Clamp(_resizeStartWidth - delta, MinSidebarWidth, MaxSidebarWidth);
            Left  = _resizeStartLeft - (newWidth - _resizeStartWidth);
            Width = newWidth;
        }
    }

    private void ResizeGrip_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_resizing) return;
        _resizing = false;
        ResizeGrip.ReleaseMouseCapture();
        _settings.SidebarWidthDip = Width;
        _settingsSvc.Save(_settings);
        _attachSvc?.UpdateWidth(Width);
    }

    // ── Quick links ───────────────────────────────────────────────────────

    private static readonly (string Label, string Path)[] QuickLinkDefs =
    {
        ("💻  This PC",       "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}"),
        ("⚙  Control Panel", "::{26EE0668-A00A-44D7-9371-BEB064C98683}"),
    };

    public void ApplyQuickLinks()
    {
        QuickLinksTopContent.Children.Clear();
        QuickLinksBottomContent.Children.Clear();

        bool[] enabled = { _settings.ShowThisPC, _settings.ShowControlPanel };
        bool anyEnabled = enabled.Any(v => v);

        bool showTop    = anyEnabled && _settings.QuickLinksPosition == QuickLinkPosition.Top;
        bool showBottom = anyEnabled && _settings.QuickLinksPosition == QuickLinkPosition.Bottom;
        QuickLinksTopPanel.Visibility    = showTop    ? Visibility.Visible : Visibility.Collapsed;
        QuickLinksBottomPanel.Visibility = showBottom ? Visibility.Visible : Visibility.Collapsed;
        if (!anyEnabled) return;

        var target = showTop ? QuickLinksTopContent : QuickLinksBottomContent;
        for (int i = 0; i < QuickLinkDefs.Length; i++)
        {
            if (!enabled[i]) continue;
            var (label, path) = QuickLinkDefs[i];
            var btn = new Button
            {
                Content                    = label,
                Tag                        = path,
                HorizontalAlignment        = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Height          = 24,
                Margin          = new Thickness(0, 1, 0, 1),
                Cursor          = Cursors.Hand,
                Background      = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground      = TryFindResource("Theme.SecondaryText") as Brush ?? Brushes.LightGray,
                FontSize        = 11,
                Padding         = new Thickness(8, 0, 0, 0),
            };
            btn.Click += QuickLink_Click;
            target.Children.Add(btn);
        }
    }

    private void QuickLink_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string path })
            _attachSvc?.NavigateTo(path);
    }

    // ── Header drag → move Explorer ───────────────────────────────────────

    private bool  _headerDragging;
    private Point _headerDragOrigin;   // screen pixels at drag start

    private void Header_DragStart(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        // Don't start drag if a button inside the header was clicked
        if (e.OriginalSource is System.Windows.Controls.Button ||
            (e.OriginalSource as System.Windows.FrameworkElement)
                ?.FindAncestorOrSelf<System.Windows.Controls.Button>() != null)
            return;
        _headerDragging   = true;
        _headerDragOrigin = PointToScreen(e.GetPosition(this));
        HeaderBorder.CaptureMouse();
        e.Handled = true;
    }

    private void Header_DragMove(object sender, MouseEventArgs e)
    {
        if (!_headerDragging || e.LeftButton != MouseButtonState.Pressed) return;
        Point current = PointToScreen(e.GetPosition(this));
        double dx = current.X - _headerDragOrigin.X;
        double dy = current.Y - _headerDragOrigin.Y;
        _headerDragOrigin = current;
        _attachSvc?.MoveExplorer(dx, dy);
    }

    private void Header_DragEnd(object sender, MouseButtonEventArgs e)
    {
        _headerDragging = false;
        HeaderBorder.ReleaseMouseCapture();
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
        if (_contextNode is { IsGroup: false } n)
            System.Diagnostics.Process.Start("explorer.exe", n.Path);
    }

    private void CtxRemove_Click(object sender, RoutedEventArgs e)
    {
        if (_contextNode is { } n)
            RemoveFolderNode(n);
    }

    private void RemoveFolderNode(FolderNode node)
    {
        // SAFETY: this removes only the sidebar bookmark from our settings JSON.
        // No filesystem operations are performed — no files or folders are ever deleted.
        if (node.IsGroup)
        {
            _settings.GroupNames.Remove(node.Path);
            // Move the group's direct children back to root
            foreach (var p in _settings.Placements)
            {
                if (p.ParentPath != null &&
                    p.ParentPath.Equals(node.Path, StringComparison.OrdinalIgnoreCase))
                    p.ParentPath = null;
            }
            _settings.Placements.RemoveAll(
                p => p.Path.Equals(node.Path, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            _settings.CustomFolders.RemoveAll(
                cf => cf.Path.Equals(node.Path, StringComparison.OrdinalIgnoreCase));
            if (!_settings.RemovedPaths.Contains(node.Path))
                _settings.RemovedPaths.Add(node.Path);
        }

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
        if (n.IsGroup)
        {
            _settings.GroupNames[n.Path] = newName;
        }
        else
        {
            var cf = _settings.CustomFolders
                .FirstOrDefault(f => f.Path.Equals(n.Path, StringComparison.OrdinalIgnoreCase));
            if (cf != null) cf.DisplayName = newName;
        }

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
