using DocMgr.Models.Shared;
using DocMgr.Models.SystemSettings;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.Interfaces;

namespace DocMgr.Services.SystemSettings
{
    public class ApprovalWorkflowService : IApprovalWorkflowService
    {
        private readonly IApprovalWorkflowRepository _repository;
        private readonly object _seedLock = new();
        private bool _seedEnsured;

        public ApprovalWorkflowService(IApprovalWorkflowRepository repository)
        {
            _repository = repository;
        }

        public IReadOnlyList<ApprovalBusinessTypeDefinition> GetBusinessTypeCatalog() =>
            ApprovalWorkflowCatalog.BusinessTypes;

        public async Task EnsureSeededAsync()
        {
            if (_seedEnsured)
            {
                return;
            }

            int count = await _repository.CountAsync();
            if (count > 0)
            {
                _seedEnsured = true;
                return;
            }

            lock (_seedLock)
            {
                // 双重检查由 Count 再查一次在锁外已完成；此处直接写入默认规则。
            }

            count = await _repository.CountAsync();
            if (count == 0)
            {
                var defaults = ApprovalWorkflowSeedSupport.CreateDefaultRules(DateTime.Now);
                await _repository.AddRangeAsync(defaults);
            }

            _seedEnsured = true;
        }

        public async Task<IReadOnlyList<ApprovalWorkflowRule>> GetRulesAsync(string? businessType = null)
        {
            await EnsureSeededAsync();
            if (string.IsNullOrWhiteSpace(businessType))
            {
                return await _repository.GetAllAsync();
            }

            return await _repository.GetByBusinessTypeAsync(businessType);
        }

        public async Task<ApprovalWorkflowRule?> GetRuleAsync(int id)
        {
            await EnsureSeededAsync();
            return await _repository.GetByIdAsync(id);
        }

        public async Task SaveRuleAsync(ApprovalWorkflowRule rule, User operatorUser)
        {
            ArgumentNullException.ThrowIfNull(rule);
            ArgumentNullException.ThrowIfNull(operatorUser);
            EnsureSystemAdministrator(operatorUser);
            await EnsureSeededAsync();

            string error = ValidateRule(rule);
            if (!string.IsNullOrWhiteSpace(error))
            {
                throw new InvalidOperationException(error);
            }

            NormalizeRule(rule);
            DateTime now = DateTime.Now;
            if (rule.Id <= 0)
            {
                rule.CreatedAt = now;
                rule.UpdatedAt = now;
                await _repository.AddAsync(rule);
                return;
            }

            var existing = await _repository.GetByIdAsync(rule.Id)
                ?? throw new InvalidOperationException("规则不存在或已删除。");

            existing.BusinessType = rule.BusinessType;
            existing.RuleName = rule.RuleName;
            existing.Priority = rule.Priority;
            existing.ConditionLogic = rule.ConditionLogic;
            existing.Condition1FieldKey = rule.Condition1FieldKey;
            existing.Condition1Value = rule.Condition1Value;
            existing.Condition2FieldKey = rule.Condition2FieldKey;
            existing.Condition2Value = rule.Condition2Value;
            existing.EnableDeptHead = rule.EnableDeptHead;
            existing.EnableArchiveRoomHead = rule.EnableArchiveRoomHead;
            existing.EnableProductionHead = rule.EnableProductionHead;
            existing.EnableArchiveDeputyPresident = rule.EnableArchiveDeputyPresident;
            existing.EnableProductionVicePresident = rule.EnableProductionVicePresident;
            existing.DefaultDeptHeadUserId = rule.DefaultDeptHeadUserId;
            existing.DefaultArchiveRoomHeadUserId = rule.DefaultArchiveRoomHeadUserId;
            existing.DefaultProductionHeadUserId = rule.DefaultProductionHeadUserId;
            existing.DefaultArchiveDeputyPresidentUserId = rule.DefaultArchiveDeputyPresidentUserId;
            existing.DefaultProductionVicePresidentUserId = rule.DefaultProductionVicePresidentUserId;
            existing.IsEnabled = rule.IsEnabled;
            existing.UpdatedAt = now;
            await _repository.UpdateAsync(existing);
        }

        public async Task DeleteRuleAsync(int id, User operatorUser)
        {
            ArgumentNullException.ThrowIfNull(operatorUser);
            EnsureSystemAdministrator(operatorUser);

            var existing = await _repository.GetByIdAsync(id)
                ?? throw new InvalidOperationException("规则不存在或已删除。");

            await _repository.DeleteAsync(existing);
        }

        public async Task ResetToDefaultsAsync(User operatorUser)
        {
            ArgumentNullException.ThrowIfNull(operatorUser);
            EnsureSystemAdministrator(operatorUser);

            var defaults = ApprovalWorkflowSeedSupport.CreateDefaultRules(DateTime.Now);
            await _repository.ReplaceAllAsync(defaults);
            _seedEnsured = true;
        }

        public async Task<string> ExportAllRulesJsonAsync(User operatorUser)
        {
            ArgumentNullException.ThrowIfNull(operatorUser);
            EnsureSystemAdministrator(operatorUser);
            await EnsureSeededAsync();

            var rules = await _repository.GetAllAsync();
            var package = new ApprovalWorkflowRulesExportPackage
            {
                SchemaVersion = ApprovalWorkflowRulesExportPackage.CurrentSchemaVersion,
                ExportedAt = DateTime.UtcNow,
                Rules = rules.Select(ApprovalWorkflowRuleExportItem.FromEntity).ToList()
            };

            return System.Text.Json.JsonSerializer.Serialize(
                package,
                new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                });
        }

        public async Task ImportAllRulesFromJsonAsync(string json, User operatorUser)
        {
            ArgumentNullException.ThrowIfNull(operatorUser);
            EnsureSystemAdministrator(operatorUser);

            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidOperationException("导入文件内容为空。");
            }

            ApprovalWorkflowRulesExportPackage? package;
            try
            {
                package = System.Text.Json.JsonSerializer.Deserialize<ApprovalWorkflowRulesExportPackage>(
                    json,
                    new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                    });
            }
            catch (System.Text.Json.JsonException ex)
            {
                throw new InvalidOperationException("导入文件不是有效的 JSON：" + ex.Message);
            }

            if (package == null)
            {
                throw new InvalidOperationException("导入文件解析结果为空。");
            }

            if (package.SchemaVersion <= 0
                || package.SchemaVersion > ApprovalWorkflowRulesExportPackage.CurrentSchemaVersion)
            {
                throw new InvalidOperationException(
                    $"不支持的导出包版本（{package.SchemaVersion}），当前支持 1～{ApprovalWorkflowRulesExportPackage.CurrentSchemaVersion}。");
            }

            if (package.Rules == null || package.Rules.Count == 0)
            {
                throw new InvalidOperationException("导入包中没有任何规则。");
            }

            DateTime now = DateTime.Now;
            var entities = new List<ApprovalWorkflowRule>(package.Rules.Count);
            for (int i = 0; i < package.Rules.Count; i++)
            {
                var item = package.Rules[i]
                    ?? throw new InvalidOperationException($"第 {i + 1} 条规则为空。");
                var entity = item.ToEntity(now);
                NormalizeRule(entity);
                string error = ValidateRule(entity);
                if (!string.IsNullOrWhiteSpace(error))
                {
                    throw new InvalidOperationException($"第 {i + 1} 条规则无效：{error}");
                }

                entities.Add(entity);
            }

            await _repository.ReplaceAllAsync(entities);
            _seedEnsured = true;
        }

        public async Task<ApprovalChainResolution> ResolveAsync(
            ApprovalChainResolveRequest request,
            IReadOnlyList<User> users)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(users);

            await EnsureSeededAsync();

            string businessType = request.BusinessType?.Trim() ?? string.Empty;
            var rules = (await _repository.GetByBusinessTypeAsync(businessType))
                .Where(item => item.IsEnabled)
                .OrderBy(item => item.Priority)
                .ThenBy(item => item.Id)
                .ToList();

            ApprovalWorkflowRule? matched = rules.FirstOrDefault(rule =>
                ApprovalWorkflowMatchingSupport.IsRuleMatch(rule, request.FieldValues));

            if (matched == null)
            {
                return BuildEmptyResolution(businessType);
            }

            return BuildResolution(matched, users, request.ApplicantDept);
        }

        public bool IsTerminalWorkflowStatus(int status) =>
            status is ApplicationWorkflowStatus.Completed
                or ApplicationWorkflowStatus.Withdrawn
                or ApplicationWorkflowStatus.ForceWithdrawn;

        private static ApprovalChainResolution BuildEmptyResolution(string businessType) =>
            new()
            {
                BusinessType = businessType,
                MatchedRuleId = null,
                MatchedRuleName = string.Empty
            };

        private static ApprovalChainResolution BuildResolution(
            ApprovalWorkflowRule rule,
            IReadOnlyList<User> users,
            string? applicantDept)
        {
            return new ApprovalChainResolution
            {
                BusinessType = rule.BusinessType,
                MatchedRuleId = rule.Id,
                MatchedRuleName = rule.RuleName,
                DeptHead = BuildSlot(
                    ApprovalWorkflowDomainValues.NodeDeptHead,
                    ApprovalWorkflowDomainValues.DisplayDeptHead,
                    rule.EnableDeptHead,
                    rule.DefaultDeptHeadUserId,
                    users,
                    applicantDept),
                ArchiveRoomHead = BuildSlot(
                    ApprovalWorkflowDomainValues.NodeArchiveRoomHead,
                    ApprovalWorkflowDomainValues.DisplayArchiveRoomHead,
                    rule.EnableArchiveRoomHead,
                    rule.DefaultArchiveRoomHeadUserId,
                    users,
                    applicantDept),
                ProductionHead = BuildSlot(
                    ApprovalWorkflowDomainValues.NodeProductionHead,
                    ApprovalWorkflowDomainValues.DisplayProductionHead,
                    rule.EnableProductionHead,
                    rule.DefaultProductionHeadUserId,
                    users,
                    applicantDept),
                ArchiveDeputyPresident = BuildSlot(
                    ApprovalWorkflowDomainValues.NodeArchiveDeputyPresident,
                    ApprovalWorkflowDomainValues.DisplayArchiveDeputyPresident,
                    rule.EnableArchiveDeputyPresident,
                    rule.DefaultArchiveDeputyPresidentUserId,
                    users,
                    applicantDept),
                ProductionVicePresident = BuildSlot(
                    ApprovalWorkflowDomainValues.NodeProductionVicePresident,
                    ApprovalWorkflowDomainValues.DisplayProductionVicePresident,
                    rule.EnableProductionVicePresident,
                    rule.DefaultProductionVicePresidentUserId,
                    users,
                    applicantDept)
            };
        }

        private static ApprovalSignerSlot BuildSlot(
            string nodeKey,
            string displayName,
            bool enabled,
            int? userId,
            IReadOnlyList<User> users,
            string? applicantDept)
        {
            string name = enabled
                ? ApprovalWorkflowMatchingSupport.ResolveRealName(users, userId, applicantDept, nodeKey)
                : string.Empty;

            return new ApprovalSignerSlot
            {
                NodeKey = nodeKey,
                DisplayName = displayName,
                IsEnabled = enabled,
                DefaultUserId = userId,
                DefaultRealName = name
            };
        }

        private static void EnsureSystemAdministrator(User user)
        {
            string role = user.Role?.Trim() ?? string.Empty;
            if (!string.Equals(role, "Administrator", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(role, "管理员", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("仅系统管理员可维护审核审批配置。");
            }
        }

        private static void NormalizeRule(ApprovalWorkflowRule rule)
        {
            rule.BusinessType = rule.BusinessType?.Trim() ?? string.Empty;
            rule.RuleName = rule.RuleName?.Trim() ?? string.Empty;
            rule.ConditionLogic = string.Equals(
                    rule.ConditionLogic?.Trim(),
                    ApprovalWorkflowDomainValues.ConditionLogicOr,
                    StringComparison.Ordinal)
                ? ApprovalWorkflowDomainValues.ConditionLogicOr
                : ApprovalWorkflowDomainValues.ConditionLogicAnd;
            rule.Condition1FieldKey = rule.Condition1FieldKey?.Trim() ?? string.Empty;
            rule.Condition1Value = rule.Condition1Value?.Trim() ?? string.Empty;
            rule.Condition2FieldKey = rule.Condition2FieldKey?.Trim() ?? string.Empty;
            rule.Condition2Value = rule.Condition2Value?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(rule.Condition1FieldKey))
            {
                rule.Condition1Value = string.Empty;
            }

            if (string.IsNullOrWhiteSpace(rule.Condition2FieldKey))
            {
                rule.Condition2Value = string.Empty;
            }
        }

        private static string ValidateRule(ApprovalWorkflowRule rule)
        {
            if (string.IsNullOrWhiteSpace(rule.RuleName))
            {
                return "请填写规则名称。";
            }

            var business = ApprovalWorkflowCatalog.FindBusinessType(rule.BusinessType);
            if (business == null)
            {
                return "业务类型无效。";
            }

            if (!rule.EnableDeptHead
                && !rule.EnableArchiveRoomHead
                && !rule.EnableProductionHead
                && !rule.EnableArchiveDeputyPresident
                && !rule.EnableProductionVicePresident)
            {
                return "请至少启用一个签批节点。";
            }

            string? conditionError = ValidateCondition(business, rule.Condition1FieldKey, rule.Condition1Value, "条件1");
            if (!string.IsNullOrWhiteSpace(conditionError))
            {
                return conditionError;
            }

            conditionError = ValidateCondition(business, rule.Condition2FieldKey, rule.Condition2Value, "条件2");
            if (!string.IsNullOrWhiteSpace(conditionError))
            {
                return conditionError;
            }

            bool has1 = !string.IsNullOrWhiteSpace(rule.Condition1FieldKey);
            bool has2 = !string.IsNullOrWhiteSpace(rule.Condition2FieldKey);
            if (has1 && has2
                && string.Equals(rule.Condition1FieldKey.Trim(), rule.Condition2FieldKey.Trim(), StringComparison.Ordinal))
            {
                return "两个条件字段不能相同。";
            }

            return string.Empty;
        }

        private static string? ValidateCondition(
            ApprovalBusinessTypeDefinition business,
            string? fieldKey,
            string? value,
            string label)
        {
            if (string.IsNullOrWhiteSpace(fieldKey))
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return $"{label}已填写取值，请先选择字段。";
                }

                return null;
            }

            var field = business.ConditionFields.FirstOrDefault(item =>
                string.Equals(item.FieldKey, fieldKey.Trim(), StringComparison.Ordinal));
            if (field == null)
            {
                return $"{label}字段不在当前业务白名单内。";
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                return $"请填写{label}的匹配值。";
            }

            if (field.Options.Count > 0
                && !field.Options.Any(option => string.Equals(option.Value, value.Trim(), StringComparison.Ordinal)))
            {
                return $"{label}的取值不在可选列表中。";
            }

            return null;
        }
    }
}
