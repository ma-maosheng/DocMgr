using System.IO;
using System.Collections.ObjectModel;
using System.Windows.Input;
using DocMgr.Services.Interfaces;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.YearlyArchive
{
    public class ArchiveRegisterSimulationViewModel : ViewModelBase
    {
        private const string DefaultChecklistText = "尚未执行立档测试。可点击「自动立档测试」或「立档测试_cursor」。";

        private readonly IArchiveRegisterSimulationService _simulationService;
        private readonly IArchiveFilingCursorTestService _cursorFilingTestService;
        private readonly IArchiveRegisterService _archiveRegisterService;
        private readonly IUserContextService _userContextService;
        private readonly IDialogService _dialogService;
        private bool _isRunning;
        private string _resultSummary = "尚未生成模拟登记数据。";
        private string _automationChecklistText = DefaultChecklistText;

        public ArchiveRegisterSimulationViewModel(
            IArchiveRegisterSimulationService simulationService,
            IArchiveFilingCursorTestService cursorFilingTestService,
            IArchiveRegisterService archiveRegisterService,
            IUserContextService userContextService,
            IDialogService dialogService)
        {
            _simulationService = simulationService;
            _cursorFilingTestService = cursorFilingTestService;
            _archiveRegisterService = archiveRegisterService;
            _userContextService = userContextService;
            _dialogService = dialogService;

            GeneratedFormNos = new ObservableCollection<string>();
            SimulationScenarios =
            [
                "生成5个硬盘借出业务：使用资料室库存真实硬盘，让 mxc 持有 5 块借出硬盘。",
                "单一模拟介质：适合测试模拟立档。",
                "单一电子介质：适合测试电子立档。",
                "硬盘+证明材料：适合测试混合立档。",
                "外来电子资料：适合测试外来资料立档。",
                "多介质综合成果（单一电子介质类型）：适合测试复杂并入与立档。",
                "复杂电子介质申请单（所有登录用户）：以当前登录用户为申请人，按登记操作台路径生成；资料室管理员另可使用下方批量测试按钮。",
                "立档测试_cursor：按介质整批入袋（与立档页第二步规则一致），逐条预览并提交电子立档，并自动为模拟介质新建档案盒。"
            ];

            GenerateFiveHardDiskBorrowCommand = new RelayCommand(
                async _ => await GenerateFiveHardDiskBorrowAsync(),
                _ => !IsRunning && IsArchiveAdminSimulationToolsVisible);
            GenerateCommand = new RelayCommand(
                async _ => await GenerateAsync(),
                _ => !IsRunning && IsArchiveAdminSimulationToolsVisible);
            GenerateComplexElectronicCommand = new RelayCommand(async _ => await GenerateComplexElectronicAsync(), _ => CanRunApplicantSimulation);
            AutoApproveSubmittedCommand = new RelayCommand(
                async _ => await AutoApproveSubmittedAsync(),
                _ => !IsRunning && IsArchiveAdminSimulationToolsVisible);
            RunAutomatedFilingTestCommand = new RelayCommand(
                async _ => await RunAutomatedFilingTestAsync(),
                _ => !IsRunning && IsArchiveAdminSimulationToolsVisible);
            RunCursorFilingTestCommand = new RelayCommand(
                async _ => await RunCursorFilingTestAsync(),
                _ => !IsRunning && IsArchiveAdminSimulationToolsVisible);
            ExportChecklistCommand = new RelayCommand(
                async _ => await ExportChecklistAsync(),
                _ => !IsRunning && IsArchiveAdminSimulationToolsVisible && HasChecklistContent());
            ClearCommand = new RelayCommand(
                async _ => await ClearAsync(),
                _ => !IsRunning && IsArchiveAdminSimulationToolsVisible);
        }

        /// <summary>申请人可用的申请模拟（复杂电子申请单等）。</summary>
        private bool CanRunApplicantSimulation => !IsRunning;

        /// <summary>资料室资料管理员专用：借出业务、批量审批、立档测试、清理等。</summary>
        public bool IsArchiveAdminSimulationToolsVisible
            => _archiveRegisterService.IsArchiveAdminUser(_userContextService.CurrentUser);

        public ObservableCollection<string> GeneratedFormNos { get; }

        public IReadOnlyList<string> SimulationScenarios { get; }

        public ICommand GenerateFiveHardDiskBorrowCommand { get; }
        public ICommand GenerateCommand { get; }
        public ICommand GenerateComplexElectronicCommand { get; }
        public ICommand AutoApproveSubmittedCommand { get; }
        public ICommand RunAutomatedFilingTestCommand { get; }
        public ICommand RunCursorFilingTestCommand { get; }
        public ICommand ExportChecklistCommand { get; }
        public ICommand ClearCommand { get; }

        public bool IsRunning
        {
            get => _isRunning;
            private set
            {
                if (SetProperty(ref _isRunning, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string ResultSummary
        {
            get => _resultSummary;
            private set => SetProperty(ref _resultSummary, value);
        }

        public string AutomationChecklistText
        {
            get => _automationChecklistText;
            private set
            {
                if (SetProperty(ref _automationChecklistText, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        private bool HasChecklistContent()
            => !string.IsNullOrWhiteSpace(AutomationChecklistText)
               && !string.Equals(AutomationChecklistText, DefaultChecklistText, StringComparison.Ordinal);

        private async Task GenerateFiveHardDiskBorrowAsync()
        {
            try
            {
                IsRunning = true;
                GeneratedFormNos.Clear();

                var result = await _simulationService.GenerateFiveHardDiskBorrowBusinessesAsync(_userContextService.CurrentUser);
                foreach (var formNo in result.FormNos)
                {
                    GeneratedFormNos.Add(formNo);
                }

                ResultSummary = result.GeneratedCount == 0
                    ? "mxc 当前已持有不少于 5 块真实借出硬盘，无需新增借出业务。"
                    : $"已生成 {result.GeneratedCount} 个硬盘借出业务，mxc 现持有真实借出硬盘可用于后续测试。";
                _dialogService.ShowMessage(ResultSummary);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("生成5个硬盘借出业务失败: " + ex.Message);
            }
            finally
            {
                IsRunning = false;
            }
        }

        private async Task GenerateAsync()
        {
            try
            {
                IsRunning = true;
                GeneratedFormNos.Clear();

                var result = await _simulationService.GenerateApprovedReceivedSamplesAsync(_userContextService.CurrentUser);
                foreach (var formNo in result.FormNos)
                {
                    GeneratedFormNos.Add(formNo);
                }

                ResultSummary = $"已生成 {result.GeneratedCount} 个状态为“已审批”的模拟申请单，可直接进入资料立档测试。";
                _dialogService.ShowMessage(ResultSummary);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("模拟登记生成失败: " + ex.Message);
            }
            finally
            {
                IsRunning = false;
            }
        }

        private async Task GenerateComplexElectronicAsync()
        {
            try
            {
                IsRunning = true;
                GeneratedFormNos.Clear();

                var result = await _simulationService.GenerateComplexElectronicSamplesAsync(_userContextService.CurrentUser);
                foreach (var formNo in result.FormNos)
                {
                    GeneratedFormNos.Add(formNo);
                }

                ResultSummary = $"已生成 {result.GeneratedCount} 个复杂电子介质模拟申请单（状态：已提交），覆盖由简到繁的介质处置与立档组合场景。";
                AutomationChecklistText = result.ChecklistLines.Count > 0
                    ? string.Join(Environment.NewLine, result.ChecklistLines)
                    : DefaultChecklistText;
                _dialogService.ShowMessage(ResultSummary);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("复杂电子介质模拟登记生成失败: " + ex.Message);
            }
            finally
            {
                IsRunning = false;
            }
        }

        private async Task ClearAsync()
        {
            if (!_dialogService.ShowConfirm("确定要清理由“模拟登记”生成的申请单吗？"))
            {
                return;
            }

            try
            {
                IsRunning = true;

                var result = await _simulationService.ClearGeneratedSamplesAsync(_userContextService.CurrentUser);
                GeneratedFormNos.Clear();

                ResultSummary = result.GeneratedCount == 0
                    ? "未找到可清理的模拟数据。"
                    : $"已清理 {result.GeneratedCount} 条模拟登记记录及其关联硬盘流程数据。";

                _dialogService.ShowMessage(ResultSummary);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("模拟登记清理失败: " + ex.Message);
            }
            finally
            {
                IsRunning = false;
            }
        }

        private async Task RunAutomatedFilingTestAsync()
        {
            try
            {
                IsRunning = true;
                var result = await _simulationService.RunAutomatedFilingTestAsync(_userContextService.CurrentUser);

                ResultSummary = $"自动化立档测试完成：处理 {result.ProcessedCount} 单，成功 {result.SucceededCount} 单，失败 {result.FailedCount} 单。";
                AutomationChecklistText = string.Join(Environment.NewLine, result.ChecklistLines);
                _dialogService.ShowMessage(ResultSummary);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("自动化立档测试失败: " + ex.Message);
            }
            finally
            {
                IsRunning = false;
            }
        }

        private async Task RunCursorFilingTestAsync()
        {
            try
            {
                IsRunning = true;
                var result = await _cursorFilingTestService.RunCursorFilingTestAsync(_userContextService.CurrentUser);

                ResultSummary = $"立档测试_cursor 完成：处理 {result.ProcessedCount} 单，成功 {result.SucceededCount} 单，失败 {result.FailedCount} 单。";
                AutomationChecklistText = string.Join(Environment.NewLine, result.ChecklistLines);
                _dialogService.ShowMessage(ResultSummary);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("立档测试_cursor 失败: " + ex.Message);
            }
            finally
            {
                IsRunning = false;
            }
        }

        private async Task AutoApproveSubmittedAsync()
        {
            try
            {
                IsRunning = true;
                var result = await _simulationService.AutoApproveSubmittedApplicationsAsync(_userContextService.CurrentUser);

                ResultSummary = result.GeneratedCount == 0
                    ? "未找到状态为“已提交”的申请单。"
                    : $"已将 {result.GeneratedCount} 条“已提交”申请单自动审批为“已办结”状态。";
                _dialogService.ShowMessage(ResultSummary);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("申请单测试数据自动审批失败: " + ex.Message);
            }
            finally
            {
                IsRunning = false;
            }
        }

        private async Task ExportChecklistAsync()
        {
            if (!HasChecklistContent())
            {
                _dialogService.ShowMessage("当前没有可导出的测试清单，请先执行「自动立档测试」或「立档测试_cursor」。");
                return;
            }

            string defaultFileName = $"立档测试清单_{DateTime.Now:yyyyMMdd_HHmmss}.md";
            string? filePath = _dialogService.SaveFileDialog("Markdown 文件|*.md|文本文件|*.txt", "导出立档测试清单", defaultFileName);
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            try
            {
                string? directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await File.WriteAllTextAsync(filePath, AutomationChecklistText);
                _dialogService.ShowMessage($"测试清单已导出：{filePath}");
            }
            catch (UnauthorizedAccessException ex)
            {
                _dialogService.ShowError("导出失败（无权限）: " + ex.Message);
            }
            catch (IOException ex)
            {
                _dialogService.ShowError("导出失败（文件读写异常）: " + ex.Message);
            }
            catch (ArgumentException ex)
            {
                _dialogService.ShowError("导出失败（文件路径无效）: " + ex.Message);
            }
        }
    }
}
