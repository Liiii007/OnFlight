using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using OnFlight.App.Helpers;
using OnFlight.App.ViewModels;
using OnFlight.Contracts.Enums;
using OnFlight.Core.Settings;

namespace OnFlight.App.Views;

public partial class MainWindow : Window
{
    private const double DragThreshold = 6;
    private const double GhostOpacity = 0.85;

    private static readonly Color LightSidebarTint = Color.Parse("#F7F7FA");
    private static readonly Color DarkSidebarTint = Color.Parse("#1E1E20");
    private static readonly Color LightNewListTint = Color.Parse("#FFFFFF");
    private static readonly Color DarkNewListTint = Color.Parse("#2C2C2E");

    private bool _isShiftHeld;
    private bool _isDragging;
    private bool _dragPending;
    private Point _dragStartPoint;
    private int _dragFromIndex = -1;
    private int _dragCurrentHoverIndex = -1;
    private Border? _dragGhost;
    private TodoItemViewModel? _dragItem;

    private int _previousTabIndex;

    public MainWindow()
    {
        InitializeComponent();
        KeyDown += (_, e) => { if (e.Key is Key.LeftShift or Key.RightShift) _isShiftHeld = true; };
        KeyUp += (_, e) => { if (e.Key is Key.LeftShift or Key.RightShift) _isShiftHeld = false; };
        Deactivated += (_, _) => _isShiftHeld = false;
        Closing += OnMainWindowClosing;
        DataContext = App.Services.GetRequiredService<MainViewModel>();
        Loaded += async (_, _) =>
        {
            var vm = (MainViewModel)DataContext!;
            vm.OnBeforeItemsSwitch = OnSlideOutAsync;
            vm.OnAfterItemsLoaded = OnSlideInAsync;
            await vm.InitializeAsync();
            ApplyAcrylicForCurrentTheme();

            vm.ConfigViewModel.CycleDetected += OnCycleDetected;

            _previousTabIndex = PanelTabs.SelectedIndex;
            _previousPanelContent = (PanelTabs.SelectedItem as TabItem)?.Content as Control;
            PanelTabs.SelectionChanged += OnPanelTabSelectionChanged;
        };
        ActualThemeVariantChanged += (_, _) => ApplyAcrylicForCurrentTheme();
    }

    private async Task OnCycleDetected(string targetListName)
    {
        await ConfirmDialog.ShowAsync(
            this,
            "Circular Reference Detected",
            $"Cannot set \"{targetListName}\" as the fork target because it would create a circular dependency. Please choose a different target list.",
            new DialogButton { Text = "OK", ResultId = "ok", IsPrimary = true });
    }

    private void ApplyAcrylicForCurrentTheme()
    {
        var isDark = ActualThemeVariant == ThemeVariant.Dark;
        var sidebarTint = isDark ? DarkSidebarTint : LightSidebarTint;
        var newListTint = isDark ? DarkNewListTint : LightNewListTint;

        if (TitleBarSidebarAcrylic.Material is ExperimentalAcrylicMaterial m1)
            m1.TintColor = sidebarTint;
        if (SidebarBodyAcrylic.Material is ExperimentalAcrylicMaterial m2)
            m2.TintColor = sidebarTint;
        if (NewListBtnAcrylic.Material is ExperimentalAcrylicMaterial m3)
            m3.TintColor = newListTint;
    }

    #region Slide Animation

    private Task OnSlideOutAsync()
    {
        return SlideTransitionHelper.SlideOutAsync(ItemListControl);
    }

    private async Task OnSlideInAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);
        await SlideTransitionHelper.SlideInAsync(ItemListControl);
    }

    private Control? _previousPanelContent;

    private async void OnPanelTabSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (PanelTabs.SelectedIndex == _previousTabIndex) return;

        var oldContent = _previousPanelContent;
        _previousTabIndex = PanelTabs.SelectedIndex;

        var selectedTab = PanelTabs.SelectedItem as TabItem;
        var newContent = selectedTab?.Content as Control;
        _previousPanelContent = newContent;

        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            if (oldContent != null)
                await SlideTransitionHelper.SlideOutAsync(oldContent);

            if (newContent != null)
                await SlideTransitionHelper.SlideInAsync(newContent);
        }, DispatcherPriority.Loaded);
    }

    #endregion

    #region Drag-Reorder

    private void OnItemPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border border) return;
        if (border.DataContext is not TodoItemViewModel itemVm) return;

        CommitAnyActiveRename();

        if (itemVm.IsRenaming) return;

        var props = e.GetCurrentPoint(border).Properties;
        if (!props.IsLeftButtonPressed) return;

        if (e.Source is Button || IsChildOfButton(e.Source as Visual)) return;

        if (itemVm.IsForkChild || itemVm.IsSubTaskChild)
        {
            _dragPending = true;
            _isDragging = false;
            _dragItem = itemVm;
            e.Pointer.Capture(border);
            return;
        }

        var vm = (MainViewModel)DataContext!;
        var idx = vm.Items.IndexOf(itemVm);
        if (idx < 0) return;

        _dragPending = true;
        _isDragging = false;
        _dragStartPoint = e.GetPosition(this);
        _dragFromIndex = idx;
        _dragItem = itemVm;
        _dragCurrentHoverIndex = -1;

        e.Pointer.Capture(border);
    }

    private void CommitAnyActiveRename()
    {
        var vm = (MainViewModel)DataContext!;
        var renaming = vm.Items.FirstOrDefault(i => i.IsRenaming);
        if (renaming != null)
            vm.CommitRenameItemCommand.Execute(renaming);
    }

    private void OnEditorAreaPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        CommitAnyActiveRename();
    }

    private void OnRenameBoxPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != Visual.IsVisibleProperty) return;
        if (sender is TextBox tb && tb.IsVisible)
        {
            Dispatcher.UIThread.Post(() =>
            {
                tb.Focus();
                tb.SelectAll();
            }, DispatcherPriority.Loaded);
        }
    }

    private void OnItemPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_dragPending && !_isDragging) return;
        if (sender is not Border border) return;

        var pos = e.GetPosition(this);

        if (_dragPending && !_isDragging)
        {
            var delta = pos - _dragStartPoint;
            if (Math.Abs(delta.Y) < DragThreshold && Math.Abs(delta.X) < DragThreshold) return;

            _isDragging = true;
            _dragPending = false;
            StartDrag(border, pos);
        }

        if (_isDragging)
        {
            UpdateDragPosition(pos);
            UpdateHoverTarget(pos);
        }
    }

    private void OnItemPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        bool wasDragging = _isDragging;
        bool wasPending = _dragPending;
        var pendingItem = _dragItem;

        if (sender is Border border)
            e.Pointer.Capture(null);

        if (wasDragging)
        {
            FinishDrag();
        }
        else if (wasPending && sender is Border clickedBorder)
        {
            if (clickedBorder.DataContext is TodoItemViewModel clickedItem
                && (clickedItem.IsForkChild || clickedItem.IsSubTaskChild))
            {
                OnItemClicked(clickedBorder);
            }
            else
            {
                var hitPos = e.GetPosition(clickedBorder);
                var hitTarget = clickedBorder.InputHitTest(hitPos);

                if (hitTarget is TextBlock tb && tb.Classes.Contains("ItemTitle")
                    && tb.DataContext is TodoItemViewModel itemVm && !itemVm.IsRenaming)
                {
                    var vm = (MainViewModel)DataContext!;
                    vm.BeginRenameItemCommand.Execute(itemVm);
                }
                else
                {
                    OnItemClicked(clickedBorder);
                }
            }
        }

        _isDragging = false;
        _dragPending = false;
        _dragItem = null;
    }

    private void StartDrag(Border sourceBorder, Point startPos)
    {
        if (_dragItem == null) return;

        _dragGhost = CreateGhostBorder(_dragItem, sourceBorder.Bounds.Width);

        DragOverlayCanvas.Children.Clear();
        DragOverlayCanvas.Children.Add(_dragGhost);
        DragOverlayCanvas.IsVisible = true;

        var sourcePos = sourceBorder.TranslatePoint(new Point(0, 0), DragOverlayCanvas);
        if (sourcePos.HasValue)
        {
            Canvas.SetLeft(_dragGhost, sourcePos.Value.X);
            Canvas.SetTop(_dragGhost, sourcePos.Value.Y);
        }

        sourceBorder.Opacity = 0.3;

        UpdateDragPosition(startPos);
    }

    private void UpdateDragPosition(Point mousePos)
    {
        if (_dragGhost == null) return;

        var canvasPos = DragOverlayCanvas.TranslatePoint(new Point(0, 0), this);
        double offsetX = canvasPos.HasValue ? canvasPos.Value.X : 0;
        double offsetY = canvasPos.HasValue ? canvasPos.Value.Y : 0;

        double ghostHeight = _dragGhost.Bounds.Height > 0 ? _dragGhost.Bounds.Height : 40;
        Canvas.SetLeft(_dragGhost, mousePos.X - offsetX - _dragGhost.Bounds.Width / 2);
        Canvas.SetTop(_dragGhost, mousePos.Y - offsetY - ghostHeight / 2);
    }

    private void UpdateHoverTarget(Point mousePos)
    {
        var vm = (MainViewModel)DataContext!;
        int newHoverIndex = FindItemIndexAtPosition(mousePos);

        if (newHoverIndex != _dragCurrentHoverIndex)
        {
            ClearAllGaps();
            _dragCurrentHoverIndex = newHoverIndex;

            if (newHoverIndex >= 0 && newHoverIndex != _dragFromIndex)
            {
                ShowInsertionGap(newHoverIndex);
            }
        }
    }

    private int FindItemIndexAtPosition(Point mousePos)
    {
        var vm = (MainViewModel)DataContext!;

        var panel = ItemListControl.GetVisualDescendants()
            .OfType<StackPanel>()
            .FirstOrDefault(p => p.Parent == ItemListControl ||
                (p.Parent is Border b && b.Parent == ItemListControl));

        if (panel == null)
        {
            panel = ItemListControl.GetVisualDescendants()
                .OfType<Panel>()
                .FirstOrDefault() as StackPanel;
        }

        var itemContainers = ItemListControl.GetVisualDescendants()
            .OfType<Border>()
            .Where(b => b.Classes.Contains("DragItem") && b.DataContext is TodoItemViewModel)
            .ToList();

        for (int i = 0; i < itemContainers.Count; i++)
        {
            var container = itemContainers[i];
            var topLeft = container.TranslatePoint(new Point(0, 0), this);
            if (!topLeft.HasValue) continue;

            double top = topLeft.Value.Y;
            double bottom = top + container.Bounds.Height;
            double midY = (top + bottom) / 2;

            if (mousePos.Y < midY && (i == 0 || mousePos.Y >= GetContainerMidY(itemContainers, i - 1)))
                return i;
            if (mousePos.Y >= midY && i == itemContainers.Count - 1)
                return i;
        }

        if (itemContainers.Count > 0 && mousePos.Y < 0)
            return 0;
        if (itemContainers.Count > 0)
            return itemContainers.Count - 1;

        return -1;
    }

    private double GetContainerMidY(List<Border> containers, int index)
    {
        if (index < 0 || index >= containers.Count) return 0;
        var topLeft = containers[index].TranslatePoint(new Point(0, 0), this);
        if (!topLeft.HasValue) return 0;
        return topLeft.Value.Y + containers[index].Bounds.Height / 2;
    }

    private void ShowInsertionGap(int targetIndex)
    {
        var containers = ItemListControl.GetVisualDescendants()
            .OfType<Border>()
            .Where(b => b.Classes.Contains("DragItem") && b.DataContext is TodoItemViewModel)
            .ToList();

        if (targetIndex < 0 || targetIndex >= containers.Count) return;

        bool insertAbove = targetIndex < _dragFromIndex;

        var target = containers[targetIndex];
        var vm = target.DataContext as TodoItemViewModel;
        if (vm != null && vm.IsForkChild) return;

        if (insertAbove)
        {
            target.RenderTransform = new TranslateTransform(0, 14);
            for (int i = targetIndex + 1; i < _dragFromIndex && i < containers.Count; i++)
            {
                containers[i].RenderTransform = new TranslateTransform(0, 14);
            }
        }
        else
        {
            target.RenderTransform = new TranslateTransform(0, -14);
            for (int i = _dragFromIndex + 1; i < targetIndex && i < containers.Count; i++)
            {
                containers[i].RenderTransform = new TranslateTransform(0, -14);
            }
        }
    }

    private void ClearAllGaps()
    {
        var containers = ItemListControl.GetVisualDescendants()
            .OfType<Border>()
            .Where(b => b.Classes.Contains("DragItem"))
            .ToList();

        foreach (var c in containers)
        {
            c.RenderTransform = null;
        }
    }

    private void FinishDrag()
    {
        ClearAllGaps();

        var containers = ItemListControl.GetVisualDescendants()
            .OfType<Border>()
            .Where(b => b.Classes.Contains("DragItem") && b.DataContext is TodoItemViewModel)
            .ToList();

        if (_dragFromIndex >= 0 && _dragFromIndex < containers.Count)
        {
            containers[_dragFromIndex].Opacity = 1.0;
        }

        DragOverlayCanvas.Children.Clear();
        DragOverlayCanvas.IsVisible = false;
        _dragGhost = null;

        if (_dragCurrentHoverIndex >= 0 && _dragCurrentHoverIndex != _dragFromIndex)
        {
            var vm = (MainViewModel)DataContext!;
            vm.MoveItem(_dragFromIndex, _dragCurrentHoverIndex);
        }

        _dragFromIndex = -1;
        _dragCurrentHoverIndex = -1;
    }

    private Border CreateGhostBorder(TodoItemViewModel item, double width)
    {
        this.TryFindResource("CardBackground", ActualThemeVariant, out var bgRes);
        this.TryFindResource("PrimaryText", ActualThemeVariant, out var fgRes);

        var ghost = new Border
        {
            Width = width,
            Opacity = GhostOpacity,
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(10, 8),
            Background = bgRes as IBrush ?? Brushes.White,
            BoxShadow = BoxShadows.Parse("0 8 24 0 #40000000"),
            RenderTransform = new ScaleTransform(1.04, 1.04),
            Child = new TextBlock
            {
                Text = item.Title,
                FontSize = 13,
                Foreground = fgRes as IBrush ?? Brushes.Black,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            }
        };
        return ghost;
    }

    private static bool IsChildOfButton(Visual? visual)
    {
        while (visual != null)
        {
            if (visual is Button) return true;
            visual = visual.GetVisualParent() as Visual;
        }
        return false;
    }

    #endregion

    #region Selection via click (replaces ListBox selection)

    private void OnItemClicked(Border border)
    {
        if (border.DataContext is not TodoItemViewModel itemVm) return;
        var vm = (MainViewModel)DataContext!;
        vm.SelectedItem = itemVm;
    }

    private void OnItemDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Border border) return;
        if (border.DataContext is not TodoItemViewModel itemVm) return;
        if (itemVm.NodeType != FlowNodeType.Fork) return;

        if (e.Source is TextBlock tb && tb.Classes.Contains("ItemTitle"))
            return;

        var vm = (MainViewModel)DataContext!;
        vm.NavigateToForkTargetCommand.Execute(itemVm);
        e.Handled = true;
    }

    #endregion

    #region Running card click

    private void OnRunningCardPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Left) return;
        if (IsChildOfInteractive(e.Source as Visual)) return;

        if (sender is Border border && border.DataContext is RunningTaskInstanceViewModel instance)
        {
            var vm = (MainViewModel)DataContext!;
            vm.AttachRunningInstanceCommand.Execute(instance);
        }
    }

    private static bool IsChildOfInteractive(Visual? visual)
    {
        while (visual != null)
        {
            if (visual is Button or ToggleSwitch or CheckBox) return true;
            visual = visual.GetVisualParent() as Visual;
        }
        return false;
    }

    #endregion

    #region Existing handlers

    private void OnAddItemKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            var vm = (MainViewModel)DataContext!;
            if (vm.AddItemCommand.CanExecute(null))
                vm.AddItemCommand.Execute(null);
        }
    }

    private void OnRenameKeyDown(object? sender, KeyEventArgs e)
    {
        var vm = (MainViewModel)DataContext!;
        if (e.Key == Key.Enter)
        {
            if (vm.CommitRenameListCommand.CanExecute(null))
                vm.CommitRenameListCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            if (vm.CancelRenameListCommand.CanExecute(null))
                vm.CancelRenameListCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnItemRenameKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox tb || tb.DataContext is not TodoItemViewModel itemVm) return;
        var vm = (MainViewModel)DataContext!;
        if (e.Key == Key.Enter)
        {
            vm.CommitRenameItemCommand.Execute(itemVm);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            itemVm.IsRenaming = false;
            e.Handled = true;
        }
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void OnSidebarListTapped(object? sender, TappedEventArgs e)
    {
        var vm = (MainViewModel)DataContext!;
        if (vm.LinkedRunningInstance != null && vm.SelectedRootList != null)
        {
            vm.SwitchToDraft(vm.SelectedRootList);
        }
    }

    private void OnMinimizeClick(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnMaximizeClick(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void OnMainWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (App.IsApplicationQuitting)
            return;
        e.Cancel = true;
        Hide();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        if (_isShiftHeld)
        {
            if (Application.Current is App app)
                app.QuitApplication();
            return;
        }

        Hide();
    }

    private void OnOpenFloatingWindow(object? sender, RoutedEventArgs e)
    {
        var floatingVm = App.Services.GetRequiredService<FloatingViewModel>();
        var floatingWindow = new FloatingWindow(floatingVm);
        floatingWindow.Show();
    }

    private async void OnOpenSettings(object? sender, RoutedEventArgs e)
    {
        var settingsVm = App.Services.GetRequiredService<SettingsViewModel>();
        settingsVm.ThemeChanged += () =>
        {
            var settingsService = App.Services.GetRequiredService<ISettingsService>();
            ((App)Application.Current!).ApplyThemeFromSettings(settingsService.Current);
            ApplyAcrylicForCurrentTheme();
        };
        settingsVm.DataCleared += () =>
        {
            var vm = (MainViewModel)DataContext!;
            _ = vm.InitializeAsync();
        };
        var settingsWindow = new SettingsWindow(settingsVm);
        await settingsWindow.ShowDialog(this);
    }

    #endregion
}
