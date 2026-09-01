using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using DocMgr.ViewModels.SystemSettings;
using Microsoft.Extensions.DependencyInjection;

namespace DocMgr.Views.SystemSettings;

/// <summary>
/// 操作手册页：青绿封面、目录高亮与分节排版阅读。
/// </summary>
public partial class HelpPage : Page
{
    private readonly IServiceScope _pageScope;
    private HelpPageViewModel? _viewModel;

    public HelpPage()
    {
        InitializeComponent();

        _pageScope = App.CurrentProvider.CreateScope();
        _viewModel = _pageScope.ServiceProvider.GetRequiredService<HelpPageViewModel>();
        DataContext = _viewModel;
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        RefreshDocument();

        Unloaded += HelpPage_Unloaded;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(HelpPageViewModel.SelectedSection)
            or nameof(HelpPageViewModel.BodyText)
            or nameof(HelpPageViewModel.SectionTitle))
        {
            RefreshDocument();
        }
    }

    private void RefreshDocument()
    {
        if (_viewModel == null)
        {
            return;
        }

        ManualViewer.Document = HelpManualFlowDocumentBuilder.Create(_viewModel.SectionTitle, _viewModel.BodyText);
    }

    private void HelpPage_Unloaded(object sender, RoutedEventArgs e)
    {
        Unloaded -= HelpPage_Unloaded;
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            _viewModel = null;
        }

        _pageScope.Dispose();
    }
}
