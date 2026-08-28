using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using DocMgr.Infrastructure.Schema;
using DocMgr.Services.Interfaces;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.SystemSettings
{
    public class AdvancedDataPageViewModel : ViewModelBase
    {
        private readonly IAdvancedDataService _dataService;
        private readonly ISchemaDictionaryMaintenanceService _schemaDictionaryMaintenance;
        private readonly IDatabaseBackupService _databaseBackupService;
        private readonly IUserContextService _userContext;
        private readonly IDialogService _dialogService;

        private List<TableBrowseEntryDto> _tables = new();
        private TableBrowseEntryDto? _selectedTable;
        private DataView? _recordsView;
        private List<TableFieldStructureDto> _tableFields = new();
        private TableFieldStructureDto? _selectedField;
        private object? _selectedRecord;
        private string _statusText = "请选择数据表";
        private string _tablePolicyText = "浏览模式：可查看全部表；维护操作受白名单控制";
        private TableBrowseInfoDto? _tableBrowseInfo;
        private string _fieldDisplayName = string.Empty;
        private bool _isBusy;
        private bool _isDisposed;
        private string _dictionaryPathText = string.Empty;
        private string _dictionaryStatusText = "可在 UI 中维护字段显示名，并与 SchemaDictionary.yaml 双向同步";
        private int _currentPage = 1;
        private int _pageSize = DefaultPageSize;
        private int _totalCount;
        private string _goToPageText = "1";

        private const int DefaultPageSize = 100;
        private const int ExportRowLimitPromptThreshold = 1000;

        public AdvancedDataPageViewModel(
            IAdvancedDataService dataService,
            ISchemaDictionaryMaintenanceService schemaDictionaryMaintenance,
            IDatabaseBackupService databaseBackupService,
            IUserContextService userContext,
            IDialogService dialogService)
        {
            _dataService = dataService;
            _schemaDictionaryMaintenance = schemaDictionaryMaintenance;
            _databaseBackupService = databaseBackupService;
            _userContext = userContext;
            _dialogService = dialogService;

            RefreshCommand = new RelayCommand(async _ => await RefreshGridAsync(), _ => !IsBusy);
            DeleteSelectedCommand = new RelayCommand(async _ => await DeleteSelectedAsync(), _ => !IsBusy && CanDeleteSelected());
            ClearTableCommand = new RelayCommand(async _ => await ClearTableAsync(), _ => !IsBusy && CanClearTable());
            SaveFieldDisplayNameCommand = new RelayCommand(async _ => await SaveFieldDisplayNameAsync(), _ => !IsBusy && CanSaveFieldDisplayName());
            SyncModelToDictionaryCommand = new RelayCommand(async _ => await SyncModelToDictionaryAsync(), _ => !IsBusy);
            ExportDisplayNamesToDictionaryCommand = new RelayCommand(async _ => await ExportDisplayNamesToDictionaryAsync(), _ => !IsBusy);
            ApplyDictionaryToDatabaseCommand = new RelayCommand(async _ => await ApplyDictionaryToDatabaseAsync(), _ => !IsBusy);
            ExportToExcelCommand = new RelayCommand(async _ => await ExportToExcelAsync(), _ => !IsBusy && HasSelectedTable);
            BackupDatabaseCommand = new RelayCommand(async _ => await BackupDatabaseAsync(), _ => !IsBusy);
            RestoreDatabaseCommand = new RelayCommand(async _ => await RestoreDatabaseAsync(), _ => !IsBusy);
            FirstPageCommand = new RelayCommand(async _ => await GoToPageAsync(1), _ => !IsBusy && CanGoPrevious);
            PreviousPageCommand = new RelayCommand(async _ => await GoToPageAsync(CurrentPage - 1), _ => !IsBusy && CanGoPrevious);
            NextPageCommand = new RelayCommand(async _ => await GoToPageAsync(CurrentPage + 1), _ => !IsBusy && CanGoNext);
            LastPageCommand = new RelayCommand(async _ => await GoToPageAsync(TotalPages), _ => !IsBusy && CanGoNext);
            GoToPageCommand = new RelayCommand(async _ => await GoToPageFromInputAsync(), _ => !IsBusy && HasSelectedTable);

            PageSizeOptions = new List<int> { 50, 100, 200, 500 };

            DictionaryPathText = _schemaDictionaryMaintenance.GetActiveDictionaryPath();
            LoadTables();
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    RaiseCommandStateChanged();
                }
            }
        }

        public List<TableBrowseEntryDto> Tables
        {
            get => _tables;
            set => SetProperty(ref _tables, value);
        }

        public TableBrowseEntryDto? SelectedTable
        {
            get => _selectedTable;
            set
            {
                if (SetProperty(ref _selectedTable, value))
                {
                    SelectedRecord = null;
                    CurrentPage = 1;
                    TotalCount = 0;
                    OnPropertyChanged(nameof(HasSelectedTable));
                    NotifyPaginationChanged();
                    RaiseCommandStateChanged();
                    _ = OnSelectedTableChangedAsync();
                }
            }
        }

        public DataView? RecordsView
        {
            get => _recordsView;
            set => SetProperty(ref _recordsView, value);
        }

        public List<TableFieldStructureDto> TableFields
        {
            get => _tableFields;
            set => SetProperty(ref _tableFields, value);
        }

        public TableFieldStructureDto? SelectedField
        {
            get => _selectedField;
            set
            {
                if (SetProperty(ref _selectedField, value))
                {
                    _ = OnSelectedFieldChangedAsync();
                    RaiseCommandStateChanged();
                }
            }
        }

        public object? SelectedRecord
        {
            get => _selectedRecord;
            set
            {
                if (SetProperty(ref _selectedRecord, value))
                {
                    RaiseCommandStateChanged();
                }
            }
        }

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public string TablePolicyText
        {
            get => _tablePolicyText;
            set => SetProperty(ref _tablePolicyText, value);
        }

        public TableBrowseInfoDto? TableBrowseInfo
        {
            get => _tableBrowseInfo;
            set
            {
                if (SetProperty(ref _tableBrowseInfo, value))
                {
                    OnPropertyChanged(nameof(HasTableBrowseInfo));
                }
            }
        }

        public bool HasTableBrowseInfo => TableBrowseInfo != null;

        public string FieldDisplayName
        {
            get => _fieldDisplayName;
            set
            {
                if (SetProperty(ref _fieldDisplayName, value))
                {
                    RaiseCommandStateChanged();
                }
            }
        }

        public string DictionaryPathText
        {
            get => _dictionaryPathText;
            set => SetProperty(ref _dictionaryPathText, value);
        }

        public string DictionaryStatusText
        {
            get => _dictionaryStatusText;
            set => SetProperty(ref _dictionaryStatusText, value);
        }

        /// <summary>当前 SQLite 库路径，便于确认备份/还原目标。</summary>
        public string DatabasePathText => _databaseBackupService.DatabasePath;

        /// <summary>共享库时提醒其他终端先退出。</summary>
        public string DatabaseBackupHintText => _databaseBackupService.IsNetworkPath
            ? "当前为共享数据库。备份/还原前请先让其他终端退出；覆盖安装只替换程序文件，不要覆盖 DocMgr.db。"
            : "覆盖安装只替换程序文件即可，数据库按迁移升级，不会冲库。不要把安装目录里的 DocMgr.db 一并覆盖。";

        public RelayCommand RefreshCommand { get; }
        public RelayCommand DeleteSelectedCommand { get; }
        public RelayCommand ClearTableCommand { get; }
        public RelayCommand SaveFieldDisplayNameCommand { get; }
        public RelayCommand SyncModelToDictionaryCommand { get; }
        public RelayCommand ExportDisplayNamesToDictionaryCommand { get; }
        public RelayCommand ApplyDictionaryToDatabaseCommand { get; }
        public RelayCommand ExportToExcelCommand { get; }
        public RelayCommand BackupDatabaseCommand { get; }
        public RelayCommand RestoreDatabaseCommand { get; }
        public RelayCommand FirstPageCommand { get; }
        public RelayCommand PreviousPageCommand { get; }
        public RelayCommand NextPageCommand { get; }
        public RelayCommand LastPageCommand { get; }
        public RelayCommand GoToPageCommand { get; }

        public List<int> PageSizeOptions { get; }

        public bool HasSelectedTable => SelectedTable != null;

        public int CurrentPage
        {
            get => _currentPage;
            private set
            {
                if (SetProperty(ref _currentPage, value))
                {
                    GoToPageText = value.ToString();
                    NotifyPaginationChanged();
                }
            }
        }

        public int PageSize
        {
            get => _pageSize;
            set
            {
                if (value <= 0 || _pageSize == value)
                {
                    return;
                }

                _pageSize = value;
                OnPropertyChanged();
                CurrentPage = 1;
                _ = ExecutePagedActionAsync(LoadCurrentPageAsync);
            }
        }

        public int TotalCount
        {
            get => _totalCount;
            private set
            {
                if (SetProperty(ref _totalCount, value))
                {
                    NotifyPaginationChanged();
                }
            }
        }

        public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);

        public string PageInfo => !HasSelectedTable
            ? string.Empty
            : TotalCount == 0
                ? "暂无记录"
                : $"第 {CurrentPage} / {TotalPages} 页，本页 {RecordsView?.Count ?? 0} 条，共 {TotalCount} 条";

        public bool CanGoPrevious => HasSelectedTable && CurrentPage > 1;

        public bool CanGoNext => HasSelectedTable && CurrentPage < TotalPages;

        public string GoToPageText
        {
            get => _goToPageText;
            set => SetProperty(ref _goToPageText, value);
        }

        public bool CanEditFieldDisplayName => true;

        private void LoadTables()
        {
            Tables = _dataService.GetManageableTables();
            RaiseCommandStateChanged();
        }

        private async Task OnSelectedTableChangedAsync()
        {
            if (_isDisposed) return;
            try
            {
                await RefreshGridAsync();
            }
            catch (ObjectDisposedException)
            {
                // 异步刷新期间页面已被释放（如用户已切走），忽略即可。
            }
            catch (Exception ex)
            {
                if (!_isDisposed)
                {
                    _dialogService.ShowError($"加载数据失败: {ex.Message}");
                }
            }
        }

        private async Task OnSelectedFieldChangedAsync()
        {
            if (_isDisposed) return;
            if (SelectedField == null)
            {
                FieldDisplayName = string.Empty;
                return;
            }

            try
            {
                IsBusy = true;
                _dialogService.SetBusyState(true);

                var definition = await _dataService.GetFieldDomainDefinitionAsync(SelectedField.EntityName, SelectedField.FieldName);
                FieldDisplayName = definition?.DisplayName ?? SelectedField.DisplayName;
            }
            catch (ObjectDisposedException)
            {
                // 异步加载字段显示名期间页面已被释放，忽略即可。
            }
            catch (Exception ex)
            {
                if (!_isDisposed)
                {
                    _dialogService.ShowError($"加载字段显示名失败: {ex.Message}");
                }
            }
            finally
            {
                _dialogService.SetBusyState(false);
                IsBusy = false;
                RaiseCommandStateChanged();
            }
        }

        private bool CanDeleteSelected()
            => SelectedRecord != null
               && SelectedTable != null
               && _dataService.CanMaintainTable(SelectedTable.EntityTypeName);

        private bool CanSaveFieldDisplayName()
            => CanEditFieldDisplayName && SelectedField != null && !string.IsNullOrWhiteSpace(FieldDisplayName);

        private bool CanClearTable()
        {
            if (SelectedTable is not TableBrowseEntryDto selected)
            {
                return false;
            }

            return _dataService.CanMaintainTable(selected.EntityTypeName);
        }

        private static void RaiseCommandStateChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }

        private async Task RefreshGridAsync()
        {
            if (_isDisposed) return;
            if (SelectedTable is not TableBrowseEntryDto selected)
            {
                RecordsView = null;
                StatusText = "请选择数据表";
                TablePolicyText = "浏览模式：可查看全部表；维护操作受白名单控制";
                TableBrowseInfo = null;
                SelectedRecord = null;
                TotalCount = 0;
                CurrentPage = 1;
                return;
            }

            try
            {
                IsBusy = true;
                _dialogService.SetBusyState(true);

                CurrentPage = 1;

                var structures = await _dataService.LoadTableStructureAsync(selected.EntityTypeName);
                TableFields = structures;

                TablePolicyText = _dataService.CanMaintainTable(selected.EntityTypeName)
                    ? "当前表：可维护（允许删除/清空）"
                    : "当前表：只读浏览（禁用删除/清空）";
                TableBrowseInfo = _dataService.GetTableBrowseInfo(selected.EntityTypeName);
                SelectedRecord = null;
                SelectedField = null;

                await LoadCurrentPageAsync();
            }
            catch (ObjectDisposedException)
            {
                // 异步刷新表格期间页面已被释放，忽略即可。
            }
            catch (Exception ex)
            {
                if (!_isDisposed)
                {
                    _dialogService.ShowError($"加载数据失败: {ex.Message}");
                }
            }
            finally
            {
                _dialogService.SetBusyState(false);
                IsBusy = false;
                RaiseCommandStateChanged();
            }
        }

        private async Task LoadCurrentPageAsync()
        {
            if (_isDisposed) return;
            if (SelectedTable is not TableBrowseEntryDto selected)
            {
                RecordsView = null;
                TotalCount = 0;
                UpdateStatusText();
                return;
            }

            var requestedPage = Math.Max(1, CurrentPage);

            try
            {
                var pageResult = await _dataService.LoadTableDataPageAsync(
                    selected.EntityTypeName,
                    requestedPage,
                    PageSize);

                TotalCount = pageResult.TotalCount;
                var totalPages = TotalPages;
                if (TotalCount > 0 && requestedPage > totalPages)
                {
                    requestedPage = totalPages;
                    pageResult = await _dataService.LoadTableDataPageAsync(
                        selected.EntityTypeName,
                        requestedPage,
                        PageSize);
                }

                CurrentPage = TotalCount == 0 ? 1 : requestedPage;
                RecordsView = pageResult.Data.DefaultView;
                SelectedRecord = null;
                UpdateStatusText();
            }
            catch (ObjectDisposedException)
            {
                // 异步分页加载期间页面已被释放，忽略即可。
            }
            catch (Exception ex)
            {
                if (!_isDisposed)
                {
                    var detail = ex.InnerException?.Message ?? ex.Message;
                    _dialogService.ShowError($"加载分页数据失败: {detail}");
                }
            }
        }

        private async Task ExecutePagedActionAsync(Func<Task> action)
        {
            if (_isDisposed || !HasSelectedTable)
            {
                return;
            }

            try
            {
                IsBusy = true;
                _dialogService.SetBusyState(true);
                await action();
            }
            finally
            {
                _dialogService.SetBusyState(false);
                IsBusy = false;
                RaiseCommandStateChanged();
            }
        }

        private void UpdateStatusText()
        {
            if (!HasSelectedTable)
            {
                StatusText = "请选择数据表";
                return;
            }

            StatusText = TotalCount == 0
                ? "共 0 条记录"
                : $"共 {TotalCount} 条记录（{PageInfo}）";
            OnPropertyChanged(nameof(PageInfo));
        }

        private async Task GoToPageAsync(int page)
        {
            if (!HasSelectedTable)
            {
                return;
            }

            var targetPage = Math.Clamp(page, 1, TotalPages);
            if (targetPage == CurrentPage && RecordsView != null)
            {
                return;
            }

            await ExecutePagedActionAsync(async () =>
            {
                CurrentPage = targetPage;
                await LoadCurrentPageAsync();
            });
        }

        private async Task GoToPageFromInputAsync()
        {
            if (!HasSelectedTable)
            {
                return;
            }

            if (!int.TryParse(GoToPageText?.Trim(), out var page))
            {
                _dialogService.ShowMessage("请输入有效的页码。", "提示");
                GoToPageText = CurrentPage.ToString();
                return;
            }

            await GoToPageAsync(page);
        }

        private void NotifyPaginationChanged()
        {
            OnPropertyChanged(nameof(TotalPages));
            OnPropertyChanged(nameof(PageInfo));
            OnPropertyChanged(nameof(CanGoPrevious));
            OnPropertyChanged(nameof(CanGoNext));
            RaiseCommandStateChanged();
        }

        public void MarkDisposed()
        {
            _isDisposed = true;
        }

        private async Task SaveFieldDisplayNameAsync()
        {
            if (SelectedField == null)
            {
                _dialogService.ShowMessage("请先选择一个字段。", "提示");
                return;
            }

            try
            {
                IsBusy = true;
                _dialogService.SetBusyState(true);

                var targetPath = ResolveTargetDictionaryPath(allowSaveDialog: true);
                if (string.IsNullOrWhiteSpace(targetPath))
                {
                    return;
                }

                var entityName = SelectedField.EntityName;
                var fieldName = SelectedField.FieldName;

                await _dataService.SaveFieldDisplayNameAsync(
                    entityName,
                    fieldName,
                    FieldDisplayName);

                await _schemaDictionaryMaintenance.MergeFieldDisplayNameToDictionaryAsync(
                    entityName,
                    fieldName,
                    FieldDisplayName,
                    targetPath);

                DictionaryPathText = targetPath;
                DictionaryStatusText = $"字段显示名已保存，并写回字典：{entityName}.{fieldName}";

                if (SelectedTable != null)
                {
                    TableFields = await _dataService.LoadTableStructureAsync(SelectedTable.EntityTypeName);
                    SelectedField = TableFields.Find(f =>
                        string.Equals(f.EntityName, entityName, StringComparison.Ordinal)
                        && string.Equals(f.FieldName, fieldName, StringComparison.Ordinal));
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"保存字段显示名失败: {ex.Message}");
            }
            finally
            {
                _dialogService.SetBusyState(false);
                IsBusy = false;
                RaiseCommandStateChanged();
            }
        }

        private async Task SyncModelToDictionaryAsync()
        {
            var targetPath = ResolveTargetDictionaryPath(allowSaveDialog: true);
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                return;
            }

            try
            {
                IsBusy = true;
                _dialogService.SetBusyState(true);

                var result = await _schemaDictionaryMaintenance.SyncModelToDictionaryAsync(targetPath);
                DictionaryPathText = result.DictionaryPath;
                DictionaryStatusText = result.Summary;
                _dialogService.ShowMessage(result.Summary, "同步完成");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"同步 EF 模型到字典失败: {ex.Message}");
            }
            finally
            {
                _dialogService.SetBusyState(false);
                IsBusy = false;
                RaiseCommandStateChanged();
            }
        }

        private async Task ExportDisplayNamesToDictionaryAsync()
        {
            var targetPath = ResolveTargetDictionaryPath(allowSaveDialog: true);
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                return;
            }

            try
            {
                IsBusy = true;
                _dialogService.SetBusyState(true);

                var result = await _schemaDictionaryMaintenance.ExportDatabaseDisplayNamesToDictionaryAsync(targetPath);
                DictionaryPathText = result.DictionaryPath;
                DictionaryStatusText = result.Summary;
                _dialogService.ShowMessage(result.Summary, "导出完成");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"导出显示名到字典失败: {ex.Message}");
            }
            finally
            {
                _dialogService.SetBusyState(false);
                IsBusy = false;
                RaiseCommandStateChanged();
            }
        }

        private async Task ApplyDictionaryToDatabaseAsync()
        {
            if (!_dialogService.ShowConfirm(
                    "将使用当前字典文件中的字段显示名覆盖数据库 FieldDomainDefinitions。\n\n域值选项不会被修改，是否继续？",
                    "从字典重置显示名"))
            {
                return;
            }

            try
            {
                IsBusy = true;
                _dialogService.SetBusyState(true);

                var result = await _schemaDictionaryMaintenance.ApplyDictionaryDisplayNamesToDatabaseAsync();
                DictionaryStatusText = result.Summary;

                if (SelectedTable != null)
                {
                    TableFields = await _dataService.LoadTableStructureAsync(SelectedTable.EntityTypeName);
                }

                _dialogService.ShowMessage(result.Summary, "重置完成");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"从字典重置显示名失败: {ex.Message}");
            }
            finally
            {
                _dialogService.SetBusyState(false);
                IsBusy = false;
                RaiseCommandStateChanged();
            }
        }

        private async Task BackupDatabaseAsync()
        {
            string defaultName = $"DocMgr-backup-{DateTime.Now:yyyyMMdd-HHmmss}.db";
            string? savePath = _dialogService.SaveFileDialog(
                "SQLite 数据库|*.db|所有文件|*.*",
                "备份当前数据库",
                defaultName);
            if (string.IsNullOrWhiteSpace(savePath))
            {
                return;
            }

            try
            {
                IsBusy = true;
                _dialogService.SetBusyState(true);
                await _databaseBackupService.BackupToFileAsync(savePath);
                _dialogService.ShowMessage($"已备份到：\n{savePath}", "备份完成");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"备份失败: {ex.Message}");
            }
            finally
            {
                _dialogService.SetBusyState(false);
                IsBusy = false;
                RaiseCommandStateChanged();
            }
        }

        private async Task RestoreDatabaseAsync()
        {
            if (_databaseBackupService.IsNetworkPath
                && !_dialogService.ShowConfirm(
                    "当前使用共享数据库。还原会覆盖该共享库。\n\n请确认其他终端已退出后再继续。是否继续选择备份文件？",
                    "共享库还原"))
            {
                return;
            }

            string? sourcePath = _dialogService.OpenFileDialog(
                "SQLite 数据库|*.db|所有文件|*.*",
                "选择要还原的备份文件");
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                return;
            }

            if (!_dialogService.ShowConfirm(
                    $"将用所选备份覆盖当前数据库：\n{_databaseBackupService.DatabasePath}\n\n覆盖后无法撤销（请先自行备份），还原完成后程序将退出。是否继续？",
                    "确认还原"))
            {
                return;
            }

            try
            {
                IsBusy = true;
                _dialogService.SetBusyState(true);
                await _databaseBackupService.RestoreFromFileAsync(sourcePath);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"还原失败: {ex.Message}");
                return;
            }
            finally
            {
                _dialogService.SetBusyState(false);
                IsBusy = false;
                RaiseCommandStateChanged();
            }

            _dialogService.ShowMessage("数据库已还原。程序即将退出，请重新打开以加载还原后的数据。", "还原完成");
            Application.Current.Shutdown();
        }

        private async Task ExportToExcelAsync()
        {
            if (SelectedTable is not TableBrowseEntryDto selected)
            {
                _dialogService.ShowMessage("请先选择数据表。", "提示");
                return;
            }

            int? maxRows = null;
            if (TotalCount > ExportRowLimitPromptThreshold)
            {
                var exportLimited = _dialogService.ShowConfirm(
                    $"当前表共有 {TotalCount} 条记录，超过 {ExportRowLimitPromptThreshold} 行。\n\n是否仅导出前 {ExportRowLimitPromptThreshold} 行？\n\n【是】仅导出前 {ExportRowLimitPromptThreshold} 行\n【否】导出全部记录",
                    "导出确认");
                if (exportLimited)
                {
                    maxRows = ExportRowLimitPromptThreshold;
                }
            }

            var folderPath = _dialogService.PickFolder("选择导出文件夹");
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                return;
            }

            var fileName = _dataService.BuildExportFileName(selected.EntityTypeName);
            var filePath = Path.Combine(folderPath, fileName);

            try
            {
                IsBusy = true;
                _dialogService.SetBusyState(true);

                await _dataService.ExportTableToExcelAsync(filePath, selected.EntityTypeName, maxRows);

                var exportedCount = maxRows.HasValue
                    ? Math.Min(maxRows.Value, TotalCount)
                    : TotalCount;
                _dialogService.ShowMessage(
                    $"导出完成：\n{filePath}\n\n共导出 {exportedCount} 条记录。",
                    "完成");
            }
            catch (IOException ex)
            {
                _dialogService.ShowError($"导出失败: {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                _dialogService.ShowError($"导出失败: {ex.Message}");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"导出失败: {ex.Message}");
            }
            finally
            {
                _dialogService.SetBusyState(false);
                IsBusy = false;
                RaiseCommandStateChanged();
            }
        }

        private string? ResolveTargetDictionaryPath(bool allowSaveDialog)
        {
            if (SchemaDictionaryPathSupport.TryResolveDevelopmentDictionaryPath(out var developmentPath))
            {
                return developmentPath;
            }

            if (allowSaveDialog)
            {
                return _dialogService.SaveFileDialog(
                    "YAML 文件|*.yaml",
                    "选择数据字典文件",
                    "SchemaDictionary.yaml");
            }

            return SchemaDictionaryPathSupport.ResolvePreferredWritableDictionaryPath();
        }

        private async Task DeleteSelectedAsync()
        {
            if (SelectedRecord == null || SelectedTable is not TableBrowseEntryDto selected)
            {
                _dialogService.ShowMessage("请先选择一条记录。");
                return;
            }

            var entityTypeName = selected.EntityTypeName;

            if (!_dataService.CanMaintainTable(entityTypeName))
            {
                _dialogService.ShowError("当前表为只读浏览表，不允许执行删除操作。", "禁止操作");
                return;
            }

            if (selected.TableName == "Users")
            {
                var currentUserId = _userContext.CurrentUser?.Id;
                var selectedId = GetSelectedRecordIdValue();

                if (selectedId is int id && currentUserId == id)
                {
                    _dialogService.ShowError("非法操作：不允许删除当前登录的管理员账号！", "安全警告");
                    return;
                }
            }

            if (!_dialogService.ShowConfirm("确定要永久删除这条记录吗？此操作不可恢复！", "确认删除"))
            {
                return;
            }

            try
            {
                IsBusy = true;
                _dialogService.SetBusyState(true);

                var id = GetSelectedRecordIdValue() ?? throw new InvalidOperationException("无法定位数据主键(Id)");
                await _dataService.DeleteRecordAsync(entityTypeName, id!);

                await ExecutePagedActionAsync(LoadCurrentPageAsync);
                _dialogService.ShowMessage("删除成功。");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"删除失败: {ex.Message}\n可能存在外键依赖，请先删除相关联的数据。");
            }
            finally
            {
                _dialogService.SetBusyState(false);
                IsBusy = false;
            }
        }

        private async Task ClearTableAsync()
        {
            if (SelectedTable is not TableBrowseEntryDto selected)
            {
                return;
            }

            var entityTypeName = selected.EntityTypeName;
            var tableName = selected.TableName;

            if (!_dataService.CanMaintainTable(entityTypeName))
            {
                _dialogService.ShowError($"当前表 {tableName} 属于只读浏览，不允许清空。", "禁止操作");
                return;
            }

            if (!_dialogService.ShowConfirm(
                    $"严重警告：您即将清空【{selected.TableName}】表中的所有数据！\n\n数据一旦清空将无法找回！\n\n确认要继续吗？",
                    "高危操作确认"))
            {
                return;
            }

            if (!_dialogService.ShowConfirm("再次确认：真的要删除所有数据吗？", "最终确认"))
            {
                return;
            }

            try
            {
                IsBusy = true;
                _dialogService.SetBusyState(true);

                await _dataService.ClearTableAsync(entityTypeName);
                await RefreshGridAsync();
                _dialogService.ShowMessage("表已清空。", "完成");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"清空失败: {ex.Message}\n通常是因为其他表存在对外键的引用，请按照依赖顺序清空数据。");
            }
            finally
            {
                _dialogService.SetBusyState(false);
                IsBusy = false;
                RaiseCommandStateChanged();
            }
        }

        private object? GetSelectedRecordIdValue()
        {
            if (SelectedRecord is not DataRowView rowView)
            {
                return null;
            }

            if (!rowView.Row.Table.Columns.Contains("Id"))
            {
                return null;
            }

            var idValue = rowView["Id"];
            return idValue == DBNull.Value ? null : idValue;
        }
    }
}
