using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using DocMgr.Models.SystemSettings;

namespace DocMgr.Services.Interfaces
{
    /// <summary>
    /// 高级数据维护服务契约：数据一致性校验、修复与批量维护操作。
    /// </summary>
    public interface IAdvancedDataService
    {
        // 获取支持管理的表名列表 (显示名称 -> 实体类型)
        List<TableBrowseEntryDto> GetManageableTables();

        // 判断指定表是否允许维护（删除/清空）
        bool CanMaintainTable(string entityTypeName);

        // 获取数据表浏览说明（中文名、关联关系、维护提示等）
        TableBrowseInfoDto? GetTableBrowseInfo(string entityTypeName);

        // 加载指定表的所有数据
        Task<DataTable> LoadTableDataAsync(string entityTypeName);

        // 分页加载指定表数据
        Task<AdvancedDataTablePageDto> LoadTableDataPageAsync(string entityTypeName, int pageIndex, int pageSize);

        // 删除单条记录
        Task DeleteRecordAsync(string entityTypeName, object recordId);

        // 清空整张表 (慎用)
        Task ClearTableAsync(string entityTypeName);

        // 加载指定表的字段结构信息
        Task<List<TableFieldStructureDto>> LoadTableStructureAsync(string entityTypeName);

        // 获取字段域值定义（不存在时返回 null）
        Task<FieldDomainDefinitionDto?> GetFieldDomainDefinitionAsync(string entityName, string fieldName);

        // 新增或更新字段域值定义
        Task<FieldDomainDefinitionDto> SaveFieldDomainDefinitionAsync(string entityName, string fieldName, string displayName, bool isDomainEnabled);

        // 读取字段域值选项
        Task<List<FieldDomainOptionDto>> GetFieldDomainOptionsAsync(int definitionId);

        // 新增或更新字段域值选项
        Task<FieldDomainOptionDto> SaveFieldDomainOptionAsync(int definitionId, int? optionId, string scope, string optionValue, string optionLabel, bool isEnabled, int sortOrder);

        // 删除字段域值选项
        Task DeleteFieldDomainOptionAsync(int optionId);

        // 读取已启用的字段域值（供业务页下拉绑定复用）
        Task<List<string>> GetEnabledDomainValuesAsync(string entityName, string fieldName, string? scope = null);

        // 仅更新字段显示名（不修改域值开关）
        Task<FieldDomainDefinitionDto> SaveFieldDisplayNameAsync(string entityName, string fieldName, string displayName);

        /// <summary>
        /// 构建导出 Excel 的默认文件名（物理表名_中文表名.xlsx）。
        /// </summary>
        string BuildExportFileName(string entityTypeName);

        /// <summary>
        /// 将指定表导出为 Excel；前两行分别为英文字段名与中文字段名。
        /// </summary>
        /// <param name="maxRowCount">为 null 时导出全部记录；否则最多导出指定行数。</param>
        Task ExportTableToExcelAsync(string filePath, string entityTypeName, int? maxRowCount);
    }
}
