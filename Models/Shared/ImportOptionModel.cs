namespace DocMgr.Models.Shared
{
    /// <summary>
    /// 导入模式枚举
    /// </summary>
    public enum ImportMode
    {
        Append,    // 追加
        Recreate   // 重建（覆盖）
    }

    /// <summary>
    /// 导入选项模型
    /// </summary>
    public class ImportOptionModel
    {
        public ImportOptionModel(string tableName)
        {
            TableName = tableName;
            SelectedMode = ImportMode.Append;
        }

        public string TableName { get; }

        public ImportMode SelectedMode { get; set; }
    }
}