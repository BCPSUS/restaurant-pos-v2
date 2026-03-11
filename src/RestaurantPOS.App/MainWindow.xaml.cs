using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RestaurantPOS.Application.Services;
using RestaurantPOS.Domain.Entities;
using RestaurantPOS.Domain.Enums;
using RestaurantPOS.Infrastructure.Database;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
// ✅ Avoid ambiguous MenuItem with WPF MenuItem control
using MenuItemEntity = RestaurantPOS.Domain.Entities.MenuItem;

namespace RestaurantPOS.App;

public partial class MainWindow : Window
{
    private readonly OrderService _orderService;
    private readonly MenuService _menuService;

    private readonly PosDbContext _db;

    private long? _currentOrderId;

    private string? _currentTableNo;
    private OrderType _currentOrderType = OrderType.Takeout;
    private MenuCategory? _selectedCategory;

    private OrderLineVm? _selectedLine;

    private Order? _currentOrder;

    private long? _pendingSelectLineId;

    private int _deltaAdd;
    private int _deltaVoid;

    public MainWindow(OrderService orderService, MenuService menuService, PosDbContext db)
    {
        InitializeComponent();
        _db = db;
        _orderService = orderService;
        _menuService = menuService;

        Loaded += MainWindow_Loaded;
    }

    // -----------------------------
    // Init / Load Menu
    // -----------------------------
    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadCategoriesAsync();

        LoadOrderTypeFromUi();
        RefreshOrderTypeUiState();

        RefreshOrderUi(null);
        UpdateSelectedLineHint();
        UpdateButtonStates(null);
        UpdateSendKitchenButtonText(null);

    }

    private void LoadOrderTypeFromUi()
    {
        // Safe defaults
        _currentOrderType = OrderType.Takeout;

        // CboOrderType might be null if called too early
        if (CboOrderType is not null)
        {
            // Try: SelectedItem as ComboBoxItem
            if (CboOrderType.SelectedItem is ComboBoxItem cbi && cbi.Content is not null)
            {
                var s = cbi.Content.ToString() ?? "";
                if (s.Equals("DineIn", StringComparison.OrdinalIgnoreCase)) _currentOrderType = OrderType.DineIn;
                else if (s.Equals("Delivery", StringComparison.OrdinalIgnoreCase)) _currentOrderType = OrderType.Delivery;
                else _currentOrderType = OrderType.Takeout;
            }
            else
            {
                // Fallback: SelectedValue / Text
                var s = (CboOrderType.Text ?? "").Trim();
                if (s.Equals("DineIn", StringComparison.OrdinalIgnoreCase)) _currentOrderType = OrderType.DineIn;
                else if (s.Equals("Delivery", StringComparison.OrdinalIgnoreCase)) _currentOrderType = OrderType.Delivery;
                else if (s.Equals("Takeout", StringComparison.OrdinalIgnoreCase)) _currentOrderType = OrderType.Takeout;
            }
        }

        // TxtTableNo might be null if called too early
        var tableText = TxtTableNo?.Text;
        _currentTableNo = string.IsNullOrWhiteSpace(tableText) ? null : tableText.Trim();
    }

    private void RefreshOrderTypeUiState()
    {
        // controls may be null if called too early or XAML failed to load
        if (TxtTableNo is null)
            return;

        var isDineIn = _currentOrderType == OrderType.DineIn;

        TxtTableNo.IsEnabled = isDineIn;

        if (!isDineIn)
        {
            TxtTableNo.Text = "";
            _currentTableNo = null;
        }
    }

    private async System.Threading.Tasks.Task LoadCategoriesAsync()
    {
        var cats = await _menuService.GetCategoriesAsync(CancellationToken.None);
        LstCategories.ItemsSource = cats;

        if (cats.Count > 0)
            LstCategories.SelectedIndex = 0;
        else
        {
            TxtSelectedCategory.Text = "(no categories)";
            ItemsMenu.ItemsSource = null;
        }
    }

    private async void LstCategories_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedCategory = LstCategories.SelectedItem as MenuCategory;

        if (_selectedCategory == null)
        {
            TxtSelectedCategory.Text = "(select category)";
            ItemsMenu.ItemsSource = null;
            return;
        }

        TxtSelectedCategory.Text = $"({_selectedCategory.Name})";
        var items = await _menuService.GetItemsByCategoryAsync(_selectedCategory.Id, CancellationToken.None);
        ItemsMenu.ItemsSource = items;
    }

    // -----------------------------
    // Menu Item Click -> Add to Order
    // -----------------------------
    private async void MenuItemButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        if (btn.Tag is not MenuItemEntity item) return;

        // Auto-create order if none (use current UI selection)
        if (_currentOrderId is null)
        {
            LoadOrderTypeFromUi();
            RefreshOrderTypeUiState();

            var create = await _orderService.CreateAsync(
    _currentOrderType,
    staffId: null,
    tableNo: _currentTableNo,
    CancellationToken.None);
            if (!create.Success)
            {
                MessageBox.Show(create.Error ?? "Create order failed");
                return;
            }
            _currentOrderId = create.Value;
        }

        var add = await _orderService.AddItemAsync(
            orderId: _currentOrderId.Value,
            menuItemId: item.Id,
            nameSnapshot: item.Name,
            price: item.Price,
            CancellationToken.None);

        if (!add.Success)
        {
            MessageBox.Show(add.Error ?? "Add item failed");
            return;
        }

        // If merge happened, we don’t know the exact line id here;
        // We'll just refresh and auto-select last line in the list.
        _pendingSelectLineId = null;
        await RefreshOrderAsync();
    }

    private void BtnApplyOrderType_Click(object sender, RoutedEventArgs e)
    {
        LoadOrderTypeFromUi();
        RefreshOrderTypeUiState();

        MessageBox.Show($"Applied: {_currentOrderType}" +
                        (_currentOrderType == OrderType.DineIn && !string.IsNullOrWhiteSpace(_currentTableNo)
                            ? $" (Table {_currentTableNo})"
                            : ""));
        // refresh header if an order is open
        if (_currentOrder is not null)
            RefreshOrderUi(_currentOrder);
    }

    private async void BtnSetTable_Click(object sender, RoutedEventArgs e)
    {
        if (_currentOrderId is null)
        {
            MessageBox.Show("No active order.");
            return;
        }

        LoadOrderTypeFromUi();
        RefreshOrderTypeUiState();

        if (_currentOrderType != OrderType.DineIn)
        {
            MessageBox.Show("Set Table is only for DineIn orders.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_currentTableNo))
        {
            MessageBox.Show("Please enter Table # first.");
            return;
        }

        // Find table
        var tableNo = _currentTableNo.Trim();
        var t = await _db.Tables.FirstOrDefaultAsync(x => x.TableNo == tableNo && x.IsActive, CancellationToken.None);
        if (t is null)
        {
            MessageBox.Show($"Table '{tableNo}' not found.");
            return;
        }

        // Update order's TableId
        var order = await _db.Orders.FirstOrDefaultAsync(x => x.Id == _currentOrderId.Value, CancellationToken.None);
        if (order is null)
        {
            MessageBox.Show("Order not found.");
            return;
        }

        order.TableId = t.Id;
        await _db.SaveChangesAsync(CancellationToken.None);

        await RefreshOrderAsync(); // will reload header + tableNo cache
        MessageBox.Show($"Table set to {t.TableNo}");
    }

    private void CboOrderType_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        LoadOrderTypeFromUi();
        RefreshOrderTypeUiState();

        // If an order is currently open, refresh header immediately
        if (_currentOrder is not null)
        {
            RefreshOrderUi(_currentOrder);
        }
    }

    //private OrderType PromptOrderType()
    //{
    //    var r = MessageBox.Show(
    //        "Yes = Dine In\nNo = Takeout\nCancel = Delivery\n\nChoose Order Type:",
    //        "Order Type",
    //        MessageBoxButton.YesNoCancel);

    //    if (r == MessageBoxResult.Yes) return OrderType.DineIn;
    //    if (r == MessageBoxResult.No) return OrderType.Takeout;
    //    return OrderType.Delivery;
    //}

    //private string? PromptText(string title, string label, string defaultValue = "")
    //{
    //    var win = new Window
    //    {
    //        Title = title,
    //        Width = 360,
    //        Height = 170,
    //        WindowStartupLocation = WindowStartupLocation.CenterOwner,
    //        ResizeMode = ResizeMode.NoResize,
    //        Owner = this
    //    };

    //    var root = new Grid { Margin = new Thickness(12) };
    //    root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // label
    //    root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // textbox
    //    root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // buttons

    //    var txtLabel = new TextBlock
    //    {
    //        Text = label,
    //        Margin = new Thickness(0, 0, 0, 8),
    //        FontWeight = FontWeights.SemiBold
    //    };
    //    Grid.SetRow(txtLabel, 0);

    //    var tb = new TextBox
    //    {
    //        Text = defaultValue ?? "",
    //        Height = 28
    //    };
    //    Grid.SetRow(tb, 1);

    //    var btnPanel = new StackPanel
    //    {
    //        Orientation = Orientation.Horizontal,
    //        HorizontalAlignment = HorizontalAlignment.Right,
    //        Margin = new Thickness(0, 12, 0, 0)
    //    };
    //    Grid.SetRow(btnPanel, 2);

    //    var btnOk = new Button { Content = "OK", Width = 80, Height = 30, IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
    //    var btnCancel = new Button { Content = "Cancel", Width = 80, Height = 30, IsCancel = true };

    //    string? result = null;

    //    btnOk.Click += (_, __) =>
    //    {
    //        result = tb.Text?.Trim();
    //        win.DialogResult = true;
    //        win.Close();
    //    };

    //    btnCancel.Click += (_, __) =>
    //    {
    //        win.DialogResult = false;
    //        win.Close();
    //    };

    //    btnPanel.Children.Add(btnOk);
    //    btnPanel.Children.Add(btnCancel);

    //    root.Children.Add(txtLabel);
    //    root.Children.Add(tb);
    //    root.Children.Add(btnPanel);

    //    win.Content = root;

    //    win.Loaded += (_, __) =>
    //    {
    //        tb.Focus();
    //        tb.SelectAll();
    //    };

    //    var ok = win.ShowDialog();
    //    if (ok != true) return null;

    //    return string.IsNullOrWhiteSpace(result) ? null : result;
    //}

    //private void PromptTableIfNeeded()
    //{
    //    _currentTableNo = null;

    //    if (_currentOrderType != OrderType.DineIn)
    //        return;

    //    var tableNo = PromptText("Dine In - Table", "Enter Table #", "");
    //    _currentTableNo = string.IsNullOrWhiteSpace(tableNo) ? null : tableNo.Trim();
    //}

    // -----------------------------
    // Right panel buttons
    // -----------------------------
    private async void BtnNewOrder_Click(object sender, RoutedEventArgs e)
    {
        LoadOrderTypeFromUi();
        RefreshOrderTypeUiState();

        var create = await _orderService.CreateAsync(
    _currentOrderType,
    staffId: null,
    tableNo: _currentTableNo,
    CancellationToken.None);
        if (!create.Success)
        {
            MessageBox.Show(create.Error ?? "Create order failed");
            return;
        }

        _currentOrderId = create.Value;
        _selectedLine = null;
        UpdateSelectedLineHint();

        await RefreshOrderAsync();
    }

    private async void BtnSendKitchen_Click(object sender, RoutedEventArgs e)
    {
        if (_currentOrderId is null)
        {
            MessageBox.Show("No active order");
            return;
        }

        // ✅ DineIn must have table before sending to kitchen
        if (_currentOrder is not null &&
            _currentOrder.OrderType == OrderType.DineIn &&
            !_currentOrder.TableId.HasValue)
        {
            MessageBox.Show("DineIn order must have a table before sending to kitchen.\nPlease enter Table # and click Set Table.");
            return;
        }
        // ✅ Prevent empty kitchen sync
        if (_deltaAdd == 0 && _deltaVoid == 0)
        {
            MessageBox.Show("Nothing to sync to kitchen.");
            return;
        }

        var r = await _orderService.SendToKitchenAsync(_currentOrderId.Value, CancellationToken.None);
        if (!r.Success)
        {
            MessageBox.Show(r.Error ?? "Send kitchen failed");
            return;
        }

        MessageBox.Show("Kitchen ticket generated (logs\\print_kitchen.txt)");
        await RefreshOrderAsync();
    }

    private async void BtnOpenOrder_Click(object sender, RoutedEventArgs e)
    {
        if (_currentOrderId is null)
        {
            MessageBox.Show("No active order");
            return;
        }

        var r = await _orderService.OpenOrderAsync(_currentOrderId.Value, CancellationToken.None);
        if (!r.Success)
        {
            MessageBox.Show(r.Error ?? "Open order failed");
            return;
        }

        MessageBox.Show("Order reopened. You can add items now.");
        await RefreshOrderAsync();
    }

    private async void BtnPrintReceipt_Click(object sender, RoutedEventArgs e)
    {
        if (_currentOrderId is null)
        {
            MessageBox.Show("No active order");
            return;
        }

        var r = await _orderService.PrintReceiptAsync(_currentOrderId.Value, CancellationToken.None);
        if (!r.Success)
        {
            MessageBox.Show(r.Error ?? "Print receipt failed");
            return;
        }

        MessageBox.Show("Receipt generated (logs\\print_receipt.txt)");
    }

    private void BtnClearOrder_Click(object sender, RoutedEventArgs e)
    {
        _currentOrderId = null;
        _selectedLine = null;
        UpdateSelectedLineHint();
        RefreshOrderUi(null);
        UpdateButtonStates(null);
    }

    // -----------------------------
    // Order lines selection + actions
    // -----------------------------
    private void LstOrderLines_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedLine = LstOrderLines.SelectedItem as OrderLineVm;
        UpdateSelectedLineHint();
        UpdateButtonStates(_currentOrder);
    }

    private void UpdateSelectedLineHint()
    {
        if (_selectedLine is null)
        {
            TxtSelectedLineHint.Text = "(select a line to edit)";
            return;
        }

        // base line
        var text = $"Selected: {_selectedLine.Qty} x {_selectedLine.Name}";

        // status tags
        if (_selectedLine.IsVoided)
        {
            var reason = string.IsNullOrWhiteSpace(_selectedLine.VoidReason) ? "Removed" : _selectedLine.VoidReason;
            var at = _selectedLine.VoidedAt.HasValue ? _selectedLine.VoidedAt.Value.ToLocalTime().ToString("MM-dd HH:mm") : "n/a";
            text += $"  [VOID] ({reason}, {at})";
        }
        else if (_selectedLine.IsSentToKitchen)
        {
            var at = _selectedLine.SentToKitchenAt.HasValue ? _selectedLine.SentToKitchenAt.Value.ToLocalTime().ToString("MM-dd HH:mm") : "n/a";
            text += $"  [SENT] ({at})";
        }

        TxtSelectedLineHint.Text = text;
    }

    private void UpdateButtonStates(Order? o)
    {
        var hasOrder = o is not null;

        // Bottom buttons
        BtnSendKitchen.IsEnabled = hasOrder;          // allow even in SentToKitchen (delta sync)
        BtnPrintReceipt.IsEnabled = hasOrder;

        // Open Order only when SentToKitchen
        BtnOpenOrder.IsEnabled = hasOrder && o!.Status == OrderStatus.SentToKitchen;

        // Line actions depend on selection
        if (!hasOrder || _selectedLine is null)
        {
            BtnLinePlus.IsEnabled = false;
            BtnLineMinus.IsEnabled = false;
            BtnLineRemove.IsEnabled = false;
            return;
        }

        // Voided line: no actions
        if (_selectedLine.IsVoided)
        {
            BtnLinePlus.IsEnabled = false;
            BtnLineMinus.IsEnabled = false;
            BtnLineRemove.IsEnabled = false;
            return;
        }

        // Remove allowed even if sent-to-kitchen
        BtnLineRemove.IsEnabled = true;

        // Qty change forbidden if sent-to-kitchen
        var canChangeQty = !_selectedLine.IsSentToKitchen;
        BtnLinePlus.IsEnabled = canChangeQty;
        BtnLineMinus.IsEnabled = canChangeQty;
    }

    private void UpdateSendKitchenButtonText(Order? o)
    {
        if (o is null)
        {
            _deltaAdd = 0;
            _deltaVoid = 0;
            BtnSendKitchen.Content = "Send Kitchen (Fake)";
            return;
        }

        _deltaAdd = o.Lines.Count(l => !l.IsVoided && !l.IsSentToKitchen);
        _deltaVoid = o.Lines.Count(l => l.IsVoided && l.IsSentToKitchen && !l.VoidSentToKitchen);

        BtnSendKitchen.Content = $"Send Kitchen (ADD:{_deltaAdd}  VOID:{_deltaVoid})";
    }

    private async void BtnLinePlus_Click(object sender, RoutedEventArgs e)
    {
        if (_currentOrderId is null)
        {
            MessageBox.Show("No active order");
            return;
        }
        if (_selectedLine is null)
        {
            MessageBox.Show("Select a line first");
            return;
        }

        if (_selectedLine.IsVoided)
        {
            MessageBox.Show("This line is removed.");
            return;
        }

        // ✅ Sent-to-kitchen lines cannot change qty
        if (_selectedLine.IsSentToKitchen)
        {
            MessageBox.Show("This item was sent to kitchen. Qty change is not allowed.");
            return;
        }

        // Reuse AddItemAsync to increment qty (merge logic will target editable line)
        var r = await _orderService.AddItemAsync(
            orderId: _currentOrderId.Value,
            menuItemId: _selectedLine.MenuItemId,
            nameSnapshot: _selectedLine.Name,
            price: _selectedLine.Price,
            CancellationToken.None);

        if (!r.Success)
        {
            MessageBox.Show(r.Error ?? "Plus failed");
            return;
        }
        _pendingSelectLineId = _selectedLine.LineId;

        await RefreshOrderAsync(selectLineId: _selectedLine.LineId);
    }

    private async void BtnLineMinus_Click(object sender, RoutedEventArgs e)
    {
        if (_currentOrderId is null)
        {
            MessageBox.Show("No active order");
            return;
        }
        if (_selectedLine is null)
        {
            MessageBox.Show("Select a line first");
            return;
        }

        if (_selectedLine.IsVoided)
        {
            MessageBox.Show("This line is removed.");
            return;
        }

        // ✅ Sent-to-kitchen lines cannot change qty
        if (_selectedLine.IsSentToKitchen)
        {
            MessageBox.Show("This item was sent to kitchen. Qty change is not allowed.");
            return;
        }

        var r = await _orderService.DecrementLineAsync(_currentOrderId.Value, _selectedLine.LineId, CancellationToken.None);
        if (!r.Success)
        {
            MessageBox.Show(r.Error ?? "Minus failed");
            return;
        }

        _pendingSelectLineId = _selectedLine.LineId;
        await RefreshOrderAsync(selectLineId: _pendingSelectLineId);
    }

    private async void BtnLineRemove_Click(object sender, RoutedEventArgs e)
    {
        if (_currentOrderId is null)
        {
            MessageBox.Show("No active order");
            return;
        }
        if (_selectedLine is null)
        {
            MessageBox.Show("Select a line first");
            return;
        }

        // ✅ Remove is allowed even if sent to kitchen (service will void it)
        var r = await _orderService.RemoveLineAsync(_currentOrderId.Value, _selectedLine.LineId, CancellationToken.None);
        if (!r.Success)
        {
            MessageBox.Show(r.Error ?? "Remove failed");
            return;
        }

        _pendingSelectLineId = _selectedLine.LineId;
        await RefreshOrderAsync(selectLineId: _pendingSelectLineId);
    }

    private void BtnBackOffice_Click(object sender, RoutedEventArgs e)
    {
        var scope = ((App)System.Windows.Application.Current).Services.CreateScope();
        var back = scope.ServiceProvider.GetRequiredService<BackOfficeWindow>();

        back.Owner = this;

        this.Hide();
        back.ShowDialog();
        this.Show();

        scope.Dispose();
    }


    // -----------------------------
    // Refresh Order UI
    // -----------------------------
    private async System.Threading.Tasks.Task RefreshOrderAsync(long? selectLineId = null)
    {
        if (_currentOrderId is null)
        {
            _selectedLine = null;
            UpdateSelectedLineHint();
            RefreshOrderUi(null);
            return;
        }

        var o = await _orderService.GetAsync(_currentOrderId.Value, CancellationToken.None);

        if (o is not null && o.TableId.HasValue)
        {
            var t = await _db.Tables.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == o.TableId.Value, CancellationToken.None);

            _currentTableNo = t?.TableNo; // keep as display cache
        }
        else
        {
            _currentTableNo = null;
        }

        RefreshOrderUi(o, selectLineId);
    }

    private void RefreshOrderUi(Order? o, long? selectLineId = null)
    {
        _currentOrder = o;
        if (o is null)
        {
            TxtOrderHeader.Text = "No active order";
            TxtOrderHeader.Foreground = System.Windows.Media.Brushes.Gray;

            LstOrderLines.ItemsSource = null;

            TxtSubtotal.Text = "0.00";
            TxtTax.Text = "0.00";
            TxtTotal.Text = "0.00";

            _selectedLine = null;
            UpdateSelectedLineHint();
            UpdateButtonStates(null);
            UpdateSendKitchenButtonText(null);
            return;
        }


        var menuItems = _db.MenuItems
    .AsNoTracking()
    .ToDictionary(x => x.Id, x => x);

        var vms = o.Lines
            .OrderBy(l => l.Id)
            .Select(l =>
            {
                menuItems.TryGetValue(l.MenuItemId, out var menuItem);

                return new OrderLineVm
                {
                    LineId = l.Id,
                    MenuItemId = l.MenuItemId,
                    Name = l.NameSnapshot,
                    Qty = l.Qty,
                    Price = l.Price,
                    Total = l.Total,
                    Station = menuItem?.Station ?? "",
                    IsVoided = l.IsVoided,
                    IsSentToKitchen = l.IsSentToKitchen,
                    VoidSentToKitchen = l.VoidSentToKitchen,
                    VoidedAt = l.VoidedAt,
                    VoidReason = l.VoidReason,
                    SentToKitchenAt = l.SentToKitchenAt
                };
            })
            .ToList();

        LstOrderLines.ItemsSource = vms;

        TxtSubtotal.Text = $"{o.Subtotal:0.00}";
        TxtTax.Text = $"{o.Tax:0.00}";
        TxtTotal.Text = $"{o.Total:0.00}";

        // ✅ POS auto-select logic
        var idToSelect = selectLineId ?? _pendingSelectLineId;

        if (idToSelect is not null)
        {
            var toSelect = vms.FirstOrDefault(x => x.LineId == idToSelect.Value);
            if (toSelect is not null)
                LstOrderLines.SelectedItem = toSelect;
        }
        else if (vms.Count > 0)
        {
            // default select last line
            LstOrderLines.SelectedItem = vms[^1];
        }

        _pendingSelectLineId = null;

        _selectedLine = LstOrderLines.SelectedItem as OrderLineVm;
        UpdateSelectedLineHint();

        UpdateButtonStates(o);
        UpdateSendKitchenButtonText(o);

        var tableText = (_currentOrderType == OrderType.DineIn && !string.IsNullOrWhiteSpace(_currentTableNo))
    ? $"  Table:{_currentTableNo}"
    : "";

        TxtOrderHeader.Text =
            $"Order #{o.OrderNumber} (Id:{o.Id})  Type:{_currentOrderType}{tableText}  Status:{o.Status}   |   Next Sync: ADD {_deltaAdd} / VOID {_deltaVoid}";
        TxtOrderHeader.Foreground = System.Windows.Media.Brushes.Black;
    }

    private sealed class OrderLineVm
    {
        public long LineId { get; set; }
        public long MenuItemId { get; set; }

        public string Station { get; set; } = "";
        public string Name { get; set; } = "";
        public int Qty { get; set; }
        public decimal Price { get; set; }
        public decimal Total { get; set; }

        public bool IsVoided { get; set; }

        public DateTime? VoidedAt { get; set; }
        public string? VoidReason { get; set; }
        public DateTime? SentToKitchenAt { get; set; }
        public bool IsSentToKitchen { get; set; }

        public bool VoidSentToKitchen { get; set; }
    }
}