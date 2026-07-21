using DocMgr.Models.YearlyArchive;
using DocMgr.ViewModels.Base;
using Microsoft.Extensions.DependencyInjection;

namespace DocMgr.ViewModels.YearlyArchive
{
    /// <summary>
    /// 审批出库主页面内联办理：打开选中单到下方办理区，不再弹出出库编辑窗。
    /// </summary>
    public sealed partial class ArchiveOutboundWorkbenchPageViewModel
    {
        private IServiceScope? _editingScope;
        private ArchiveOutboundViewModel? _editingViewModel;

        /// <summary>当前主页面办理区绑定的出库单 ViewModel；未打开时为 null。</summary>
        public ArchiveOutboundViewModel? EditingViewModel
        {
            get => _editingViewModel;
            private set
            {
                if (ReferenceEquals(_editingViewModel, value))
                {
                    return;
                }

                _editingViewModel = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasEditingViewModel));
            }
        }

        public bool HasEditingViewModel => EditingViewModel != null;

        /// <summary>关闭办理区。</summary>
        public DocMgr.ViewModels.Base.RelayCommand CloseEditingCommand { get; private set; } = null!;

        private void InitializeInlineEditingCommands()
        {
            CloseEditingCommand = new DocMgr.ViewModels.Base.RelayCommand(_ => CloseEditingPanel(), _ => HasEditingViewModel);
        }

        private async Task OpenForInlineEditingAsync()
        {
            if (SelectedRecord == null)
            {
                return;
            }

            await LoadEditingViewModelAsync(SelectedRecord.Id);
        }

        /// <summary>
        /// 列表筛选或选中行变化后，将办理区同步到当前选中申请单；无选中则关闭办理区。
        /// </summary>
        private async Task SyncEditingPanelToSelectionAsync()
        {
            if (!HasEditingViewModel)
            {
                return;
            }

            if (SelectedRecord == null)
            {
                CloseEditingPanel();
                return;
            }

            if (_editingViewModel != null && _editingViewModel.Record.Id == SelectedRecord.Id)
            {
                return;
            }

            await LoadEditingViewModelAsync(SelectedRecord.Id);
        }

        private async Task LoadEditingViewModelAsync(int recordId)
        {
            if (recordId <= 0)
            {
                return;
            }

            DisposeEditingViewModel();

            _editingScope = _scopeFactory.CreateScope();
            var viewModel = _editingScope.ServiceProvider.GetRequiredService<ArchiveOutboundViewModel>();
            viewModel.SetWorkspaceMode(ArchiveOutboundWorkspaceMode.Approval);
            viewModel.RequestClose += OnEditingViewModelRequestClose;

            await viewModel.InitializeAsync(recordId);
            EditingViewModel = viewModel;
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }

        private void OnEditingViewModelRequestClose(bool? result)
        {
            bool committed = _editingViewModel?.HasCommittedChanges == true;
            CloseEditingPanel();
            if (committed)
            {
                _ = LoadRecordsAsync();
            }
        }

        private void CloseEditingPanel()
        {
            DisposeEditingViewModel();
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }

        private void DisposeEditingViewModel()
        {
            if (_editingViewModel != null)
            {
                _editingViewModel.RequestClose -= OnEditingViewModelRequestClose;
            }

            EditingViewModel = null;
            _editingScope?.Dispose();
            _editingScope = null;
        }
    }
}
