using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using DocMgr.Models.SystemSettings;
using DocMgr.Services.Interfaces;
using DocMgr.Services.YearlyArchive;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.SystemSettings
{
    /// <summary>审核审批配置页：按业务维护签批规则。</summary>
    public class ApprovalWorkflowSettingsViewModel : ViewModelBase
    {
        private readonly IApprovalWorkflowService _approvalWorkflowService;
        private readonly IUserService _userService;
        private readonly IUserContextService _userContextService;
        private readonly IDialogService _dialogService;

        private bool _isInitialized;
        private ApprovalBusinessTypeDefinition? _selectedBusinessType;
        private ApprovalWorkflowRuleRow? _selectedRule;
        private bool _isEditing;
        private string _editRuleName = string.Empty;
        private int _editPriority = 100;
        private string _editConditionLogic = ApprovalWorkflowDomainValues.ConditionLogicAnd;
        private ApprovalConditionFieldDefinition? _editCondition1Field;
        private string? _editCondition1Value;
        private ApprovalConditionFieldDefinition? _editCondition2Field;
        private string? _editCondition2Value;
        private bool _editEnableDeptHead = true;
        private bool _editEnableArchiveRoomHead;
        private bool _editEnableProductionHead;
        private bool _editEnableArchiveDeputyPresident;
        private bool _editEnableProductionVicePresident;
        private UserOptionItem? _editDeptHeadUser;
        private UserOptionItem? _editArchiveRoomHeadUser;
        private UserOptionItem? _editProductionHeadUser;
        private UserOptionItem? _editArchiveDeputyPresidentUser;
        private UserOptionItem? _editProductionVicePresidentUser;
        private bool _editIsEnabled = true;
        private int _editingRuleId;

        public ApprovalWorkflowSettingsViewModel(
            IApprovalWorkflowService approvalWorkflowService,
            IUserService userService,
            IUserContextService userContextService,
            IDialogService dialogService)
        {
            _approvalWorkflowService = approvalWorkflowService;
            _userService = userService;
            _userContextService = userContextService;
            _dialogService = dialogService;

            BusinessTypes = new ObservableCollection<ApprovalBusinessTypeDefinition>(
                _approvalWorkflowService.GetBusinessTypeCatalog());
            Rules = new ObservableCollection<ApprovalWorkflowRuleRow>();
            ConditionFieldOptions = new ObservableCollection<ApprovalConditionFieldDefinition>();
            Condition1ValueOptions = new ObservableCollection<string>();
            Condition2ValueOptions = new ObservableCollection<string>();
            UserOptions = new ObservableCollection<UserOptionItem>();

            CanMaintain = ArchiveRegisterBusinessRules.IsSystemAdministrator(_userContextService.CurrentUser);

            AddRuleCommand = new RelayCommand(_ => BeginAddRule(), _ => CanMaintain && SelectedBusinessType != null && !IsEditing);
            EditRuleCommand = new RelayCommand(_ => BeginEditRule(), _ => CanMaintain && SelectedRule != null && !IsEditing);
            DeleteRuleCommand = new RelayCommand(async _ => await DeleteRuleAsync(), _ => CanMaintain && SelectedRule != null && !IsEditing);
            SaveRuleCommand = new RelayCommand(async _ => await SaveRuleAsync(), _ => CanMaintain && IsEditing);
            CancelEditCommand = new RelayCommand(_ => CancelEdit(), _ => IsEditing);
            MoveUpCommand = new RelayCommand(async _ => await MovePriorityAsync(-1), _ => CanMaintain && CanMoveSelected(-1));
            MoveDownCommand = new RelayCommand(async _ => await MovePriorityAsync(1), _ => CanMaintain && CanMoveSelected(1));
            ResetDefaultsCommand = new RelayCommand(async _ => await ResetDefaultsAsync(), _ => CanMaintain && !IsEditing);
            ExportRulesCommand = new RelayCommand(async _ => await ExportRulesAsync(), _ => !IsEditing);
            ImportRulesCommand = new RelayCommand(async _ => await ImportRulesAsync(), _ => CanMaintain && !IsEditing);
            RefreshCommand = new RelayCommand(async _ => await LoadRulesAsync(), _ => !IsEditing);
        }

        /// <summary>系统管理员可维护规则；其余角色仅浏览。</summary>
        public bool CanMaintain { get; }

        public ObservableCollection<ApprovalBusinessTypeDefinition> BusinessTypes { get; }

        public ObservableCollection<ApprovalWorkflowRuleRow> Rules { get; }

        public ObservableCollection<ApprovalConditionFieldDefinition> ConditionFieldOptions { get; }

        public ObservableCollection<string> Condition1ValueOptions { get; }

        public ObservableCollection<string> Condition2ValueOptions { get; }

        public ObservableCollection<UserOptionItem> UserOptions { get; }

        public IReadOnlyList<ConditionLogicOptionItem> ConditionLogicOptions { get; } =
        [
            new(ApprovalWorkflowDomainValues.ConditionLogicAnd, "且（AND）"),
            new(ApprovalWorkflowDomainValues.ConditionLogicOr, "或（OR）")
        ];

        public ApprovalBusinessTypeDefinition? SelectedBusinessType
        {
            get => _selectedBusinessType;
            set
            {
                if (SetProperty(ref _selectedBusinessType, value))
                {
                    _ = LoadRulesAsync();
                    RefreshConditionFields();
                }
            }
        }

                public ApprovalWorkflowRuleRow? SelectedRule
        {
            get => _selectedRule;
            set
            {
                if (SetProperty(ref _selectedRule, value) && !IsEditing)
                {
                    LoadEditorFromSelectedRule();
                }
            }
        }

        public bool IsEditing
        {
            get => _isEditing;
            private set
            {
                if (SetProperty(ref _isEditing, value))
                {
                    OnPropertyChanged(nameof(IsListEnabled));
                }
            }
        }

        public bool IsListEnabled => !IsEditing;

        public string EditRuleName
        {
            get => _editRuleName;
            set => SetProperty(ref _editRuleName, value ?? string.Empty);
        }

        public int EditPriority
        {
            get => _editPriority;
            set => SetProperty(ref _editPriority, value);
        }

        public string EditConditionLogic
        {
            get => _editConditionLogic;
            set => SetProperty(ref _editConditionLogic, value ?? ApprovalWorkflowDomainValues.ConditionLogicAnd);
        }

        public ApprovalConditionFieldDefinition? EditCondition1Field
        {
            get => _editCondition1Field;
            set
            {
                if (SetProperty(ref _editCondition1Field, value))
                {
                    RefreshValueOptions(Condition1ValueOptions, value);
                    if (value == null)
                    {
                        EditCondition1Value = null;
                    }
                }
            }
        }

        public string? EditCondition1Value
        {
            get => _editCondition1Value;
            set => SetProperty(ref _editCondition1Value, value);
        }

        public ApprovalConditionFieldDefinition? EditCondition2Field
        {
            get => _editCondition2Field;
            set
            {
                if (SetProperty(ref _editCondition2Field, value))
                {
                    RefreshValueOptions(Condition2ValueOptions, value);
                    if (value == null)
                    {
                        EditCondition2Value = null;
                    }
                }
            }
        }

        public string? EditCondition2Value
        {
            get => _editCondition2Value;
            set => SetProperty(ref _editCondition2Value, value);
        }

        public bool EditEnableDeptHead
        {
            get => _editEnableDeptHead;
            set => SetProperty(ref _editEnableDeptHead, value);
        }

        public bool EditEnableArchiveRoomHead
        {
            get => _editEnableArchiveRoomHead;
            set => SetProperty(ref _editEnableArchiveRoomHead, value);
        }

        public bool EditEnableProductionHead
        {
            get => _editEnableProductionHead;
            set => SetProperty(ref _editEnableProductionHead, value);
        }

        public bool EditEnableArchiveDeputyPresident
        {
            get => _editEnableArchiveDeputyPresident;
            set => SetProperty(ref _editEnableArchiveDeputyPresident, value);
        }

        public bool EditEnableProductionVicePresident
        {
            get => _editEnableProductionVicePresident;
            set => SetProperty(ref _editEnableProductionVicePresident, value);
        }

        public UserOptionItem? EditDeptHeadUser
        {
            get => _editDeptHeadUser;
            set => SetProperty(ref _editDeptHeadUser, value);
        }

        public UserOptionItem? EditArchiveRoomHeadUser
        {
            get => _editArchiveRoomHeadUser;
            set => SetProperty(ref _editArchiveRoomHeadUser, value);
        }

        public UserOptionItem? EditProductionHeadUser
        {
            get => _editProductionHeadUser;
            set => SetProperty(ref _editProductionHeadUser, value);
        }

        public UserOptionItem? EditArchiveDeputyPresidentUser
        {
            get => _editArchiveDeputyPresidentUser;
            set => SetProperty(ref _editArchiveDeputyPresidentUser, value);
        }

        public UserOptionItem? EditProductionVicePresidentUser
        {
            get => _editProductionVicePresidentUser;
            set => SetProperty(ref _editProductionVicePresidentUser, value);
        }

        public bool EditIsEnabled
        {
            get => _editIsEnabled;
            set => SetProperty(ref _editIsEnabled, value);
        }

        public ICommand AddRuleCommand { get; }
        public ICommand EditRuleCommand { get; }
        public ICommand DeleteRuleCommand { get; }
        public ICommand SaveRuleCommand { get; }
        public ICommand CancelEditCommand { get; }
        public ICommand MoveUpCommand { get; }
        public ICommand MoveDownCommand { get; }
        public ICommand ResetDefaultsCommand { get; }
        public ICommand ExportRulesCommand { get; }
        public ICommand ImportRulesCommand { get; }
        public ICommand RefreshCommand { get; }

        public async Task InitializeAsync()
        {
            if (_isInitialized)
            {
                return;
            }

            try
            {
                _isInitialized = true;
                ReloadUsers();
                SelectedBusinessType = BusinessTypes.FirstOrDefault();
                await LoadRulesAsync();
            }
            catch (Exception ex)
            {
                _isInitialized = false;
                string detail = ex.InnerException?.Message ?? ex.Message;
                _dialogService.ShowError(
                    $"审核审批配置初始化失败：{detail}"
                    + Environment.NewLine
                    + Environment.NewLine
                    + "若提示缺少表 ApprovalWorkflowRules，请关闭程序后重新启动以应用数据库迁移；开发期也可删库重建。");
            }
        }

        private void ReloadUsers()
        {
            UserOptions.Clear();
            UserOptions.Add(new UserOptionItem(null, "（自动解析 / 未指定）"));
            foreach (var user in _userService.GetAllUsers()
                         .Where(item => !string.IsNullOrWhiteSpace(item.RealName))
                         .OrderBy(item => item.Department)
                         .ThenBy(item => item.RealName))
            {
                string label = string.IsNullOrWhiteSpace(user.Department)
                    ? user.RealName.Trim()
                    : $"{user.RealName.Trim()}（{user.Department.Trim()}）";
                UserOptions.Add(new UserOptionItem(user.Id, label));
            }
        }

        private async Task LoadRulesAsync()
        {
            Rules.Clear();
            if (SelectedBusinessType == null)
            {
                SelectedRule = null;
                if (!IsEditing)
                {
                    LoadEditorFromSelectedRule();
                }

                return;
            }

            var rules = await _approvalWorkflowService.GetRulesAsync(SelectedBusinessType.BusinessType);
            foreach (var rule in rules.OrderBy(item => item.Priority).ThenBy(item => item.Id))
            {
                Rules.Add(ApprovalWorkflowRuleRow.FromEntity(rule, SelectedBusinessType));
            }

            SelectedRule = Rules.FirstOrDefault();
            if (!IsEditing)
            {
                // SelectedRule 未变化时仍强制同步右侧表单（例如恢复默认后首条仍同名）。
                LoadEditorFromSelectedRule();
            }
        }

        private void RefreshConditionFields()
        {
            ConditionFieldOptions.Clear();
            ConditionFieldOptions.Add(new ApprovalConditionFieldDefinition
            {
                FieldKey = string.Empty,
                DisplayName = "（无）",
                Options = Array.Empty<(string, string)>()
            });

            if (SelectedBusinessType == null)
            {
                return;
            }

            foreach (var field in SelectedBusinessType.ConditionFields)
            {
                ConditionFieldOptions.Add(field);
            }
        }

        private static void RefreshValueOptions(
            ObservableCollection<string> target,
            ApprovalConditionFieldDefinition? field)
        {
            target.Clear();
            if (field == null || string.IsNullOrWhiteSpace(field.FieldKey))
            {
                return;
            }

            foreach (var option in field.Options)
            {
                // 下拉展示优先用 Label；落库仍用 Value（编辑时可直接选中 Value）。
                string value = option.Value?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                target.Add(value);
            }
        }

        private void BeginAddRule()
        {
            if (SelectedBusinessType == null)
            {
                return;
            }

            _editingRuleId = 0;
            EditRuleName = $"新规则-{SelectedBusinessType.DisplayName}";
            EditPriority = Rules.Count == 0 ? 100 : Rules.Max(item => item.Priority) + 10;
            EditConditionLogic = ApprovalWorkflowDomainValues.ConditionLogicAnd;
            EditCondition1Field = ConditionFieldOptions.FirstOrDefault();
            EditCondition1Value = null;
            EditCondition2Field = ConditionFieldOptions.FirstOrDefault();
            EditCondition2Value = null;
            EditEnableDeptHead = true;
            EditEnableArchiveRoomHead = false;
            EditEnableProductionHead = false;
            EditEnableArchiveDeputyPresident = false;
            EditEnableProductionVicePresident = false;
            EditDeptHeadUser = UserOptions.FirstOrDefault();
            EditArchiveRoomHeadUser = UserOptions.FirstOrDefault();
            EditProductionHeadUser = UserOptions.FirstOrDefault();
            EditArchiveDeputyPresidentUser = UserOptions.FirstOrDefault();
            EditProductionVicePresidentUser = UserOptions.FirstOrDefault();
            EditIsEnabled = true;
            IsEditing = true;
        }

        private void BeginEditRule()
        {
            if (SelectedRule == null)
            {
                return;
            }

            LoadEditorFromSelectedRule();
            _editingRuleId = SelectedRule.Source.Id;
            IsEditing = true;
        }

        private void LoadEditorFromSelectedRule()
        {
            if (SelectedRule == null)
            {
                _editingRuleId = 0;
                EditRuleName = string.Empty;
                EditPriority = 100;
                EditConditionLogic = ApprovalWorkflowDomainValues.ConditionLogicAnd;
                EditCondition1Field = ConditionFieldOptions.FirstOrDefault();
                EditCondition1Value = null;
                EditCondition2Field = ConditionFieldOptions.FirstOrDefault();
                EditCondition2Value = null;
                EditEnableDeptHead = true;
                EditEnableArchiveRoomHead = false;
                EditEnableProductionHead = false;
                EditEnableArchiveDeputyPresident = false;
                EditEnableProductionVicePresident = false;
                EditDeptHeadUser = UserOptions.FirstOrDefault();
                EditArchiveRoomHeadUser = UserOptions.FirstOrDefault();
                EditProductionHeadUser = UserOptions.FirstOrDefault();
                EditArchiveDeputyPresidentUser = UserOptions.FirstOrDefault();
                EditProductionVicePresidentUser = UserOptions.FirstOrDefault();
                EditIsEnabled = true;
                return;
            }

            var rule = SelectedRule.Source;
            EditRuleName = rule.RuleName;
            EditPriority = rule.Priority;
            EditConditionLogic = string.IsNullOrWhiteSpace(rule.ConditionLogic)
                ? ApprovalWorkflowDomainValues.ConditionLogicAnd
                : rule.ConditionLogic;
            EditCondition1Field = FindFieldOption(rule.Condition1FieldKey);
            EditCondition1Value = string.IsNullOrWhiteSpace(rule.Condition1Value) ? null : rule.Condition1Value;
            EditCondition2Field = FindFieldOption(rule.Condition2FieldKey);
            EditCondition2Value = string.IsNullOrWhiteSpace(rule.Condition2Value) ? null : rule.Condition2Value;
            EditEnableDeptHead = rule.EnableDeptHead;
            EditEnableArchiveRoomHead = rule.EnableArchiveRoomHead;
            EditEnableProductionHead = rule.EnableProductionHead;
            EditEnableArchiveDeputyPresident = rule.EnableArchiveDeputyPresident;
            EditEnableProductionVicePresident = rule.EnableProductionVicePresident;
            EditDeptHeadUser = FindUserOption(rule.DefaultDeptHeadUserId);
            EditArchiveRoomHeadUser = FindUserOption(rule.DefaultArchiveRoomHeadUserId);
            EditProductionHeadUser = FindUserOption(rule.DefaultProductionHeadUserId);
            EditArchiveDeputyPresidentUser = FindUserOption(rule.DefaultArchiveDeputyPresidentUserId);
            EditProductionVicePresidentUser = FindUserOption(rule.DefaultProductionVicePresidentUserId);
            EditIsEnabled = rule.IsEnabled;
        }

        private ApprovalConditionFieldDefinition? FindFieldOption(string? fieldKey)
        {
            if (string.IsNullOrWhiteSpace(fieldKey))
            {
                return ConditionFieldOptions.FirstOrDefault();
            }

            return ConditionFieldOptions.FirstOrDefault(item =>
                       string.Equals(item.FieldKey, fieldKey.Trim(), StringComparison.Ordinal))
                   ?? ConditionFieldOptions.FirstOrDefault();
        }

        private UserOptionItem? FindUserOption(int? userId) =>
            UserOptions.FirstOrDefault(item => item.UserId == userId) ?? UserOptions.FirstOrDefault();

        private void CancelEdit()
        {
            IsEditing = false;
            _editingRuleId = 0;
            LoadEditorFromSelectedRule();
        }

        private async Task SaveRuleAsync()
        {
            var user = _userContextService.CurrentUser;
            if (user == null || SelectedBusinessType == null)
            {
                _dialogService.ShowError("当前未登录或未选择业务类型。");
                return;
            }

            var rule = new ApprovalWorkflowRule
            {
                Id = _editingRuleId,
                BusinessType = SelectedBusinessType.BusinessType,
                RuleName = EditRuleName,
                Priority = EditPriority,
                ConditionLogic = EditConditionLogic,
                Condition1FieldKey = EditCondition1Field?.FieldKey ?? string.Empty,
                Condition1Value = EditCondition1Value ?? string.Empty,
                Condition2FieldKey = EditCondition2Field?.FieldKey ?? string.Empty,
                Condition2Value = EditCondition2Value ?? string.Empty,
                EnableDeptHead = EditEnableDeptHead,
                EnableArchiveRoomHead = EditEnableArchiveRoomHead,
                EnableProductionHead = EditEnableProductionHead,
                EnableArchiveDeputyPresident = EditEnableArchiveDeputyPresident,
                EnableProductionVicePresident = EditEnableProductionVicePresident,
                DefaultDeptHeadUserId = EditDeptHeadUser?.UserId,
                DefaultArchiveRoomHeadUserId = EditArchiveRoomHeadUser?.UserId,
                DefaultProductionHeadUserId = EditProductionHeadUser?.UserId,
                DefaultArchiveDeputyPresidentUserId = EditArchiveDeputyPresidentUser?.UserId,
                DefaultProductionVicePresidentUserId = EditProductionVicePresidentUser?.UserId,
                IsEnabled = EditIsEnabled
            };

            try
            {
                await _approvalWorkflowService.SaveRuleAsync(rule, user);
                IsEditing = false;
                await LoadRulesAsync();
                _dialogService.ShowMessage("规则已保存。");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private async Task DeleteRuleAsync()
        {
            var user = _userContextService.CurrentUser;
            if (user == null || SelectedRule == null)
            {
                return;
            }

            if (!_dialogService.ShowConfirm($"确认删除规则「{SelectedRule.RuleName}」？"))
            {
                return;
            }

            try
            {
                await _approvalWorkflowService.DeleteRuleAsync(SelectedRule.Id, user);
                await LoadRulesAsync();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private bool CanMoveSelected(int direction)
        {
            if (IsEditing || SelectedRule == null || Rules.Count < 2)
            {
                return false;
            }

            int index = Rules.IndexOf(SelectedRule);
            int target = index + direction;
            return index >= 0 && target >= 0 && target < Rules.Count;
        }

        private async Task MovePriorityAsync(int direction)
        {
            var user = _userContextService.CurrentUser;
            if (user == null || SelectedRule == null || !CanMoveSelected(direction))
            {
                return;
            }

            int index = Rules.IndexOf(SelectedRule);
            var other = Rules[index + direction];
            int selectedPriority = SelectedRule.Priority;
            int otherPriority = other.Priority;
            if (selectedPriority == otherPriority)
            {
                selectedPriority = direction < 0 ? otherPriority - 1 : otherPriority + 1;
            }

            try
            {
                var selectedEntity = SelectedRule.Source;
                selectedEntity.Priority = otherPriority;
                await _approvalWorkflowService.SaveRuleAsync(selectedEntity, user);

                var otherEntity = other.Source;
                otherEntity.Priority = selectedPriority;
                await _approvalWorkflowService.SaveRuleAsync(otherEntity, user);

                await LoadRulesAsync();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private async Task ResetDefaultsAsync()
        {
            var user = _userContextService.CurrentUser;
            if (user == null)
            {
                return;
            }

            if (!_dialogService.ShowConfirm("确认将全部业务的审核审批规则恢复为系统默认？现有自定义规则将被覆盖。"))
            {
                return;
            }

            try
            {
                await _approvalWorkflowService.ResetToDefaultsAsync(user);
                CancelEdit();
                await LoadRulesAsync();
                _dialogService.ShowMessage("已恢复默认规则。");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private async Task ExportRulesAsync()
        {
            var user = _userContextService.CurrentUser;
            if (user == null)
            {
                return;
            }

            string defaultName = $"审核审批规则_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            string? filePath = _dialogService.SaveFileDialog(
                "JSON Files|*.json|All Files|*.*",
                "导出审核审批规则",
                defaultName);
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            try
            {
                string json = await _approvalWorkflowService.ExportAllRulesJsonAsync(user);
                await File.WriteAllTextAsync(filePath, json);
                _dialogService.ShowMessage($"已导出全部规则：\n{filePath}", "完成");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private async Task ImportRulesAsync()
        {
            var user = _userContextService.CurrentUser;
            if (user == null)
            {
                return;
            }

            string? filePath = _dialogService.OpenFileDialog(
                "JSON Files|*.json|All Files|*.*",
                "导入审核审批规则");
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            if (!_dialogService.ShowConfirm(
                    "确认用所选 JSON 文件覆盖当前全部审核审批规则？导入成功后现有规则将被替换，且不可自动撤销。"))
            {
                return;
            }

            try
            {
                string json = await File.ReadAllTextAsync(filePath);
                await _approvalWorkflowService.ImportAllRulesFromJsonAsync(json, user);
                CancelEdit();
                await LoadRulesAsync();
                _dialogService.ShowMessage("已覆盖导入全部审核审批规则。", "完成");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }
    }

    public sealed class ConditionLogicOptionItem
    {
        public ConditionLogicOptionItem(string code, string label)
        {
            Code = code;
            Label = label;
        }

        public string Code { get; }

        public string Label { get; }
    }

    public sealed class UserOptionItem
    {
        public UserOptionItem(int? userId, string displayName)
        {
            UserId = userId;
            DisplayName = displayName;
        }

        public int? UserId { get; }

        public string DisplayName { get; }
    }

    public sealed class ApprovalWorkflowRuleRow
    {
        public required ApprovalWorkflowRule Source { get; init; }

        public int Id => Source.Id;

        public string RuleName => Source.RuleName;

        public int Priority => Source.Priority;

        public string EnabledDisplay => Source.IsEnabled ? "启用" : "停用";

        public string ConditionSummary { get; init; } = string.Empty;

        public string NodesSummary { get; init; } = string.Empty;

        public static ApprovalWorkflowRuleRow FromEntity(
            ApprovalWorkflowRule rule,
            ApprovalBusinessTypeDefinition business)
        {
            var conditions = new List<string>();
            AppendCondition(conditions, business, rule.Condition1FieldKey, rule.Condition1Value);
            AppendCondition(conditions, business, rule.Condition2FieldKey, rule.Condition2Value);
            string logic = conditions.Count == 2
                ? ApprovalWorkflowDomainValues.ToConditionLogicDisplay(rule.ConditionLogic)
                : string.Empty;
            string conditionSummary = conditions.Count == 0
                ? "（无条件/默认）"
                : conditions.Count == 1
                    ? conditions[0]
                    : $"{conditions[0]} {logic} {conditions[1]}";

            var nodes = new List<string>();
            if (rule.EnableDeptHead) nodes.Add(ApprovalWorkflowDomainValues.DisplayDeptHead);
            if (rule.EnableArchiveRoomHead) nodes.Add(ApprovalWorkflowDomainValues.DisplayArchiveRoomHead);
            if (rule.EnableProductionHead) nodes.Add(ApprovalWorkflowDomainValues.DisplayProductionHead);
            if (rule.EnableArchiveDeputyPresident) nodes.Add(ApprovalWorkflowDomainValues.DisplayArchiveDeputyPresident);
            if (rule.EnableProductionVicePresident) nodes.Add(ApprovalWorkflowDomainValues.DisplayProductionVicePresident);

            return new ApprovalWorkflowRuleRow
            {
                Source = rule,
                ConditionSummary = conditionSummary,
                NodesSummary = nodes.Count == 0 ? "—" : string.Join("、", nodes)
            };
        }

        private static void AppendCondition(
            List<string> target,
            ApprovalBusinessTypeDefinition business,
            string fieldKey,
            string value)
        {
            if (string.IsNullOrWhiteSpace(fieldKey) || string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            var field = business.ConditionFields.FirstOrDefault(item =>
                string.Equals(item.FieldKey, fieldKey.Trim(), StringComparison.Ordinal));
            string fieldName = field?.DisplayName ?? fieldKey;
            target.Add($"{fieldName}={value.Trim()}");
        }
    }
}
