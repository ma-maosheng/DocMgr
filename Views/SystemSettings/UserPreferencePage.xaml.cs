using System;
using System.Windows;
using System.Windows.Controls;
using DocMgr.ViewModels.SystemSettings;
using Microsoft.Extensions.DependencyInjection;

namespace DocMgr.Views.SystemSettings
{
    public partial class UserPreferencePage : Page
    {
        private readonly IServiceScope _pageScope;

        public UserPreferencePage()
        {
            InitializeComponent();

            _pageScope = App.CurrentProvider.CreateScope();
            DataContext = _pageScope.ServiceProvider.GetRequiredService<UserPreferenceViewModel>();

            Loaded += UserPreferencePage_Loaded;
            Unloaded += UserPreferencePage_Unloaded;
        }

        private async void UserPreferencePage_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is UserPreferenceViewModel vm)
            {
                await vm.InitializeAsync();
            }
        }

        private void UserPreferencePage_Unloaded(object sender, RoutedEventArgs e)
        {
            Loaded -= UserPreferencePage_Loaded;
            Unloaded -= UserPreferencePage_Unloaded;
            _pageScope.Dispose();
        }
    }
}