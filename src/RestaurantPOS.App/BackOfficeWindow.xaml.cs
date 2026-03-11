using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;

namespace RestaurantPOS.App
{
    public partial class BackOfficeWindow : Window
    {
        private readonly IServiceProvider _services;

        public BackOfficeWindow(IServiceProvider services)
        {
            InitializeComponent();

            _services = services;

            MainContent.Content = new TextBlock
            {
                Text = "Back Office Home",
                FontSize = 28,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        private void BtnMenu_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = _services.GetRequiredService<MenuManagementControl>();
        }

        private void BtnReturn_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}