namespace DocMgr.Models.HardDiskMedia
{
    internal sealed class HardDiskMediaImportException : InvalidOperationException
    {
        internal HardDiskMediaImportException(string message, int? rowNumber = null, string? columnName = null)
            : base(message)
        {
            RowNumber = rowNumber;
            ColumnName = columnName;
        }

        internal int? RowNumber { get; }

        internal string? ColumnName { get; }
    }
}
