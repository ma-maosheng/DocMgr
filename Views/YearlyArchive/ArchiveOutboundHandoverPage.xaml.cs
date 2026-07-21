using DocMgr.Models.YearlyArchive;
using DocMgr.ViewModels.YearlyArchive;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;

namespace DocMgr.Views.YearlyArchive
{
    /// <summary>
    /// 兼容旧「资料出库」入口：统一跳转到审批出库页。
    /// </summary>
    public partial class ArchiveOutboundHandoverPage : Page
    {
        private readonly IServiceScope _pageScope;

        public ArchiveOutboundHandoverPage(int initialRecordId = 0)
        {
            InitializeComponent();
            _pageScope = App.CurrentProvider.CreateScope();
            var vmFactory = _pageScope.ServiceProvider
                .GetRequiredService<Func<ArchiveOutboundWorkspaceMode, int, ArchiveOutboundWorkbenchPageViewModel>>();
            // 已合并到审批出库：沿用 Approval 工作台，避免再维护独立出库页逻辑。
            DataContext = vmFactory(ArchiveOutboundWorkspaceMode.Approval, initialRecordId);
            Loaded += async (_, _) =>
            {
                if (DataContext is ArchiveOutboundWorkbenchPageViewModel vm)
                {
                    await vm.InitializeAsync();
                }
            };
            Unloaded += (_, _) => _pageScope.Dispose();
        }

        private void DgRecords_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (DataContext is ArchiveOutboundWorkbenchPageViewModel vm && vm.OpenCommand.CanExecute(null))
            {
                vm.OpenCommand.Execute(null);
            }
        }
    }
}
