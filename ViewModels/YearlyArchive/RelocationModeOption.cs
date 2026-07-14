namespace DocMgr.ViewModels.YearlyArchive
{
    public sealed class RelocationModeOption
    {
        public RelocationModeOption(string label, string value)
        {
            Label = label;
            Value = value;
        }

        public string Label { get; }

        public string Value { get; }
    }
}
