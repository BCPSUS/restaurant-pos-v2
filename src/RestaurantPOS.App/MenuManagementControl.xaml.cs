using RestaurantPOS.Application.Interfaces;
using RestaurantPOS.Domain.Entities;
using MenuItemEntity = RestaurantPOS.Domain.Entities.MenuItem;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RestaurantPOS.App
{
    public partial class MenuManagementControl : UserControl
    {
        private readonly IMenuRepository _menuRepository;

        private MenuCategory? _selectedCategory;
        private MenuItemEntity? _selectedItem;

        private bool _isNewItem = false;
        private bool _isNewCategory = false;

        private List<MenuCategory> _categories = new();
        private List<MenuItemEntity> _currentItems = new();

        private enum EditorMode
        {
            None,
            Item,
            Category
        }

        private EditorMode _editorMode = EditorMode.None;

        private const int ItemRows = 8;
        private const int ItemColumns = 3;
        private const int CategoryRows = 8;
        private const int CategoryColumns = 2;

        private const int ItemCapacity = ItemRows * ItemColumns;             // 24
        private const int CategoryCapacity = CategoryRows * CategoryColumns; // 16

        public MenuManagementControl(IMenuRepository menuRepository)
        {
            InitializeComponent();

            _menuRepository = menuRepository;

            CmbStation.Items.Clear();
            CmbStation.Items.Add("Sushi");
            CmbStation.Items.Add("Kitchen");
            CmbStation.Items.Add("Bar");

            Loaded += MenuManagementControl_Loaded;

            BtnAddNew.Click += BtnAddNew_Click;
            BtnSave.Click += BtnSave_Click;
            BtnDelete.Click += BtnDelete_Click;

            BtnAddNewCategory.Click += BtnAddNewCategory_Click;
            BtnSaveCategory.Click += BtnSaveCategory_Click;
            BtnDeleteCategory.Click += BtnDeleteCategory_Click;
        }

        private async void MenuManagementControl_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadCategoriesAsync();
            RenderCategories();
            RenderItems();

            ClearEditor();
            ClearCategoryEditor();
            ShowNoSelectionMode();

            TxtStatus.Text = "Ready";
        }

        #region Load / Render

        private async Task LoadCategoriesAsync()
        {
            var categories = await _menuRepository.GetCategoriesAsync(CancellationToken.None);

            _categories = categories
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Name)
                .ToList();
        }

        private async Task LoadItemsAsync(long categoryId)
        {
            var items = await _menuRepository.GetAllItemsByCategoryAsync(categoryId, CancellationToken.None);

            _currentItems = items
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Name)
                .ToList();
        }

        private void RenderCategories()
        {
            CategoriesGrid.Children.Clear();

            foreach (var category in _categories.Take(CategoryCapacity))
            {
                CategoriesGrid.Children.Add(BuildCategoryButton(category));
            }

            int remain = Math.Max(0, CategoryCapacity - _categories.Count);
            for (int i = 0; i < remain; i++)
            {
                CategoriesGrid.Children.Add(BuildEmptyCell());
            }
        }

        private void RenderItems()
        {
            ItemsGrid.Children.Clear();

            foreach (var item in _currentItems.Take(ItemCapacity))
            {
                ItemsGrid.Children.Add(BuildItemButton(item));
            }

            int remain = Math.Max(0, ItemCapacity - _currentItems.Count);
            for (int i = 0; i < remain; i++)
            {
                ItemsGrid.Children.Add(BuildEmptyCell());
            }

            TxtItemsHeader.Text = _selectedCategory == null
                ? "No Category Selected"
                : $"Category: {_selectedCategory.Name}";
        }

        #endregion

        #region Build Buttons

        private Button BuildCategoryButton(MenuCategory category)
        {
            var button = new Button
            {
                Tag = category,
                Margin = new Thickness(6),
                Padding = new Thickness(8),
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                BorderThickness = new Thickness(1),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Content = BuildCategoryContent(category)
            };

            ApplyCategoryButtonStyle(button, category);
            button.Click += CategoryButton_Click;

            return button;
        }

        private UIElement BuildCategoryContent(MenuCategory category)
        {
            var panel = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            panel.Children.Add(new TextBlock
            {
                Text = category.Name,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap
            });

            if (!category.IsActive)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = "[Inactive]",
                    FontSize = 11,
                    Foreground = Brushes.DarkRed,
                    Margin = new Thickness(0, 4, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Center
                });
            }

            return panel;
        }

        private void ApplyCategoryButtonStyle(Button button, MenuCategory category)
        {
            bool isSelected = _selectedCategory?.Id == category.Id && _editorMode == EditorMode.Category;

            if (isSelected)
            {
                button.Background = new SolidColorBrush(Color.FromRgb(255, 224, 178));
                button.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 152, 0));
                button.BorderThickness = new Thickness(2);
            }
            else
            {
                button.Background = new SolidColorBrush(Color.FromRgb(240, 240, 240));
                button.BorderBrush = new SolidColorBrush(Color.FromRgb(207, 207, 207));
                button.BorderThickness = new Thickness(1);
            }

            button.Opacity = category.IsActive ? 1.0 : 0.55;
        }

        private Button BuildItemButton(MenuItemEntity item)
        {
            var button = new Button
            {
                Tag = item,
                Margin = new Thickness(6),
                Padding = new Thickness(10),
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(208, 208, 208)),
                BorderThickness = new Thickness(1),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch,
                Content = BuildItemContent(item)
            };

            ApplyItemButtonStyle(button, item);
            button.Click += ItemButton_Click;

            return button;
        }

        private UIElement BuildItemContent(MenuItemEntity item)
        {
            var panel = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center
            };

            panel.Children.Add(new TextBlock
            {
                Text = item.Name,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 6)
            });

            panel.Children.Add(new TextBlock
            {
                Text = item.Price.ToString("0.00"),
                FontSize = 14,
                Foreground = Brushes.DarkGreen,
                HorizontalAlignment = HorizontalAlignment.Center
            });

            panel.Children.Add(new TextBlock
            {
                Text = $"#{item.ItemNo}",
                FontSize = 12,
                Foreground = Brushes.DimGray,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0)
            });

            panel.Children.Add(new TextBlock
            {
                Text = $"[{item.SortOrder}]",
                FontSize = 11,
                Foreground = Brushes.Gray,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0)
            });

            if (!item.IsActive)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = "[Inactive]",
                    FontSize = 11,
                    Foreground = Brushes.DarkRed,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 4, 0, 0)
                });
            }

            return panel;
        }

        private void ApplyItemButtonStyle(Button button, MenuItemEntity item)
        {
            bool isSelected = _selectedItem?.Id == item.Id && _editorMode == EditorMode.Item;

            if (isSelected)
            {
                button.Background = new SolidColorBrush(Color.FromRgb(227, 242, 253));
                button.BorderBrush = new SolidColorBrush(Color.FromRgb(33, 150, 243));
                button.BorderThickness = new Thickness(2);
            }
            else
            {
                button.Background = Brushes.White;
                button.BorderBrush = new SolidColorBrush(Color.FromRgb(208, 208, 208));
                button.BorderThickness = new Thickness(1);
            }

            button.Opacity = item.IsActive ? 1.0 : 0.55;
        }

        private Border BuildEmptyCell()
        {
            return new Border
            {
                Margin = new Thickness(6),
                Background = new SolidColorBrush(Color.FromRgb(248, 248, 248)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(234, 234, 234)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4)
            };
        }

        #endregion

        #region Click Events

        private async void CategoryButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not MenuCategory category)
                return;

            _selectedCategory = category;
            _selectedItem = null;
            _isNewItem = false;
            _isNewCategory = false;

            FillCategoryEditor(category);
            ShowCategoryEditor();

            TxtStatus.Text = $"Loading items for {category.Name}...";

            await LoadItemsAsync(category.Id);

            RenderCategories();
            RenderItems();
            ClearEditor(keepCategory: true);

            TxtStatus.Text = $"Selected category: {category.Name}";
        }

        private void ItemButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not MenuItemEntity item)
                return;

            _selectedItem = item;
            _isNewItem = false;

            FillEditor(item);
            ShowItemEditor();
            RenderItems();
            RenderCategories();

            TxtStatus.Text = $"Selected item: {item.Name}";
        }

        #endregion

        #region Editor Mode

        private void ShowNoSelectionMode()
        {
            _editorMode = EditorMode.None;
            TxtEditorModeTitle.Text = "No Selection";

            ItemEditorPanel.Visibility = Visibility.Collapsed;
            CategoryEditorPanel.Visibility = Visibility.Collapsed;

            RenderCategories();
            RenderItems();
        }

        private void ShowItemEditor()
        {
            _editorMode = EditorMode.Item;
            TxtEditorModeTitle.Text = "Item Editor";

            ItemEditorPanel.Visibility = Visibility.Visible;
            CategoryEditorPanel.Visibility = Visibility.Collapsed;
        }

        private void ShowCategoryEditor()
        {
            _editorMode = EditorMode.Category;
            TxtEditorModeTitle.Text = "Category Editor";

            ItemEditorPanel.Visibility = Visibility.Collapsed;
            CategoryEditorPanel.Visibility = Visibility.Visible;
        }

        #endregion

        #region Item Editor

        private void ClearEditor(bool keepCategory = false)
        {
            _selectedItem = null;
            _isNewItem = false;

            TxtItemId.Text = "";
            TxtItemNo.Text = "";
            TxtName.Text = "";
            TxtName2.Text = "";
            TxtPrice.Text = "";
            TxtSortOrder.Text = "";

            CmbStation.SelectedIndex = -1;
            ChkIsActive.IsChecked = true;

            if (keepCategory)
            {
                TxtCategory.Text = _selectedCategory?.Name ?? "";
            }
            else
            {
                TxtCategory.Text = "";
            }
        }

        private void FillEditor(MenuItemEntity item)
        {
            TxtItemId.Text = item.Id.ToString();
            TxtItemNo.Text = item.ItemNo;
            TxtName.Text = item.Name;
            TxtName2.Text = item.Name2;
            TxtPrice.Text = item.Price.ToString("0.00");
            TxtSortOrder.Text = item.SortOrder.ToString();
            CmbStation.SelectedItem = item.Station;
            ChkIsActive.IsChecked = item.IsActive;
            TxtCategory.Text = _selectedCategory?.Name ?? "";
        }

        #endregion

        #region Category Editor

        private void ClearCategoryEditor()
        {
            _isNewCategory = false;

            TxtCategoryId.Text = "";
            TxtCategoryName.Text = "";
            TxtCategorySortOrder.Text = "";
            ChkCategoryIsActive.IsChecked = true;
        }

        private void FillCategoryEditor(MenuCategory category)
        {
            TxtCategoryId.Text = category.Id.ToString();
            TxtCategoryName.Text = category.Name;
            TxtCategorySortOrder.Text = category.SortOrder.ToString();
            ChkCategoryIsActive.IsChecked = category.IsActive;
        }

        #endregion

        #region Item Buttons

        private async void BtnAddNew_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedCategory is null)
            {
                MessageBox.Show("Please select a category first.");
                return;
            }

            ClearEditor(keepCategory: true);
            _isNewItem = true;

            ShowItemEditor();

            var nextSortOrder = await _menuRepository.GetNextSortOrderAsync(_selectedCategory.Id, CancellationToken.None);
            TxtSortOrder.Text = nextSortOrder.ToString();

            ChkIsActive.IsChecked = true;
            TxtStatus.Text = $"Create new item in {_selectedCategory.Name}";
            TxtItemNo.Focus();

            RenderItems();
            RenderCategories();
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedCategory is null)
            {
                MessageBox.Show("Please select a category first.");
                return;
            }

            if (string.IsNullOrWhiteSpace(TxtItemNo.Text) || string.IsNullOrWhiteSpace(TxtName.Text))
            {
                MessageBox.Show("Item No and Name are required.");
                return;
            }

            if (!decimal.TryParse(TxtPrice.Text.Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, out var price) &&
                !decimal.TryParse(TxtPrice.Text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out price))
            {
                MessageBox.Show("Invalid price.");
                return;
            }

            if (!int.TryParse(TxtSortOrder.Text.Trim(), out var sortOrder))
            {
                MessageBox.Show("Invalid sort order.");
                return;
            }

            var station = CmbStation.SelectedItem?.ToString();
            if (string.IsNullOrWhiteSpace(station))
            {
                MessageBox.Show("Please select a station.");
                return;
            }

            var itemNo = TxtItemNo.Text.Trim();
            var name = TxtName.Text.Trim();
            var name2 = TxtName2.Text.Trim();

            bool duplicate = _currentItems.Any(x =>
                string.Equals(x.ItemNo, itemNo, StringComparison.OrdinalIgnoreCase) &&
                (_selectedItem == null || x.Id != _selectedItem.Id));

            if (duplicate)
            {
                MessageBox.Show("Item No already exists in this category.");
                return;
            }

            long savedItemId;

            if (_isNewItem)
            {
                var newItem = new MenuItemEntity
                {
                    CategoryId = _selectedCategory.Id,
                    ItemNo = itemNo,
                    Name = name,
                    Name2 = name2,
                    Price = price,
                    Station = station,
                    IsActive = ChkIsActive.IsChecked == true,
                    SortOrder = sortOrder
                };

                var added = await _menuRepository.AddItemAsync(newItem, CancellationToken.None);
                savedItemId = added.Id;

                TxtStatus.Text = $"New item added: {added.Name}";
            }
            else
            {
                if (_selectedItem is null)
                {
                    MessageBox.Show("Please select an item first.");
                    return;
                }

                _selectedItem.ItemNo = itemNo;
                _selectedItem.Name = name;
                _selectedItem.Name2 = name2;
                _selectedItem.Price = price;
                _selectedItem.Station = station;
                _selectedItem.IsActive = ChkIsActive.IsChecked == true;
                _selectedItem.SortOrder = sortOrder;

                var updated = await _menuRepository.UpdateItemAsync(_selectedItem, CancellationToken.None);
                savedItemId = updated.Id;

                TxtStatus.Text = $"Saved item: {updated.Name}";
            }

            await LoadItemsAsync(_selectedCategory.Id);

            _selectedItem = _currentItems.FirstOrDefault(x => x.Id == savedItemId);

            ShowItemEditor();
            RenderItems();
            RenderCategories();

            if (_selectedItem != null)
            {
                FillEditor(_selectedItem);
            }
            else
            {
                ClearEditor(keepCategory: true);
            }
        }

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedItem is null || _isNewItem)
            {
                MessageBox.Show("Please select an existing item first.");
                return;
            }

            if (_selectedCategory is null)
            {
                MessageBox.Show("Please select a category first.");
                return;
            }

            var result = MessageBox.Show(
                $"Delete item '{_selectedItem.Name}' permanently?\n\nHistorical orders will remain unchanged because order snapshots are stored separately.",
                "Confirm Permanent Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            string deletedName = _selectedItem.Name;

            await _menuRepository.DeleteItemAsync(_selectedItem.Id, CancellationToken.None);

            await LoadItemsAsync(_selectedCategory.Id);

            _selectedItem = null;
            _isNewItem = false;

            RenderItems();
            RenderCategories();
            ClearEditor(keepCategory: true);
            ShowNoSelectionMode();

            TxtStatus.Text = $"Deleted item: {deletedName}";
        }

        #endregion

        #region Category Buttons

        private async void BtnAddNewCategory_Click(object sender, RoutedEventArgs e)
        {
            _selectedCategory = null;
            _selectedItem = null;
            _isNewCategory = true;
            _isNewItem = false;

            ClearCategoryEditor();
            ClearEditor();
            ShowCategoryEditor();

            var nextSortOrder = await _menuRepository.GetNextCategorySortOrderAsync(CancellationToken.None);
            TxtCategorySortOrder.Text = nextSortOrder.ToString();
            ChkCategoryIsActive.IsChecked = true;

            _currentItems.Clear();
            RenderItems();
            RenderCategories();

            TxtItemsHeader.Text = "No Category Selected";
            TxtStatus.Text = "Create new category";

            TxtCategoryName.Focus();
        }

        private async void BtnSaveCategory_Click(object sender, RoutedEventArgs e)
        {
            string name = TxtCategoryName.Text.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Category Name is required.");
                return;
            }

            if (!int.TryParse(TxtCategorySortOrder.Text.Trim(), out int sortOrder))
            {
                MessageBox.Show("Invalid category sort order.");
                return;
            }

            bool isActive = ChkCategoryIsActive.IsChecked == true;

            bool duplicateName = _categories.Any(x =>
                string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase) &&
                (_selectedCategory == null || x.Id != _selectedCategory.Id));

            if (duplicateName)
            {
                MessageBox.Show("Category name already exists.");
                return;
            }

            if (_isNewCategory)
            {
                var newCategory = new MenuCategory
                {
                    Name = name,
                    SortOrder = sortOrder,
                    IsActive = isActive
                };

                var added = await _menuRepository.AddCategoryAsync(newCategory, CancellationToken.None);
                _selectedCategory = added;
                _isNewCategory = false;

                TxtStatus.Text = $"New category added: {added.Name}";
            }
            else
            {
                if (_selectedCategory == null)
                {
                    MessageBox.Show("Please select a category first.");
                    return;
                }

                _selectedCategory.Name = name;
                _selectedCategory.SortOrder = sortOrder;
                _selectedCategory.IsActive = isActive;

                var updated = await _menuRepository.UpdateCategoryAsync(_selectedCategory, CancellationToken.None);
                _selectedCategory = updated;

                TxtStatus.Text = $"Category saved: {updated.Name}";
            }

            await LoadCategoriesAsync();

            if (_selectedCategory != null)
            {
                await LoadItemsAsync(_selectedCategory.Id);
                FillCategoryEditor(_selectedCategory);
                TxtCategory.Text = _selectedCategory.Name;
                ShowCategoryEditor();
            }
            else
            {
                _currentItems.Clear();
            }

            RenderCategories();
            RenderItems();
        }

        private async void BtnDeleteCategory_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedCategory == null || _isNewCategory)
            {
                MessageBox.Show("Please select an existing category first.");
                return;
            }

            bool hasItems = await _menuRepository.CategoryHasItemsAsync(_selectedCategory.Id, CancellationToken.None);
            if (hasItems)
            {
                MessageBox.Show("This category still has menu items. Please move or delete items first.");
                return;
            }

            var result = MessageBox.Show(
                $"Delete category '{_selectedCategory.Name}' permanently?",
                "Confirm Delete Category",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            string deletedName = _selectedCategory.Name;

            await _menuRepository.DeleteCategoryAsync(_selectedCategory.Id, CancellationToken.None);

            _selectedCategory = null;
            _selectedItem = null;
            _isNewCategory = false;
            _isNewItem = false;

            await LoadCategoriesAsync();

            _currentItems.Clear();

            ClearCategoryEditor();
            ClearEditor();
            ShowNoSelectionMode();

            RenderCategories();
            RenderItems();

            TxtStatus.Text = $"Deleted category: {deletedName}";
        }

        #endregion
    }
}