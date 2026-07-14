using System;
using System.Windows;
using System.Windows.Controls;
using DocMgr.ViewModels.Projects;
using Microsoft.Extensions.DependencyInjection;

namespace DocMgr.Views.Projects
{
    public partial class ProjectSettingPage : Page
    {
        private readonly IServiceScope _pageScope;

        public ProjectSettingPage()
        {
            InitializeComponent();

            _pageScope = App.CurrentProvider.CreateScope();
            DataContext = _pageScope.ServiceProvider.GetRequiredService<ProjectSettingViewModel>();

            Unloaded += ProjectSettingPage_Unloaded;
        }

        private void ProjectSettingPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Unloaded -= ProjectSettingPage_Unloaded;
            _pageScope.Dispose();
        }
    }
}