namespace DocMgr.Models.Cabinets
{
    public sealed class CabinetOpenRequest
    {
        public int CabinetId { get; init; }

        public string CabinetName { get; init; } = string.Empty;

        public CabinetType CabinetType { get; init; }

        public CabinetFace Face { get; init; }

        public int LayerCount { get; init; }

        public int ColumnCount { get; init; }

        public string TargetSlotCode { get; init; } = string.Empty;

        public double WidthCm { get; init; }

        public double HeightCm { get; init; }

        public double DepthCm { get; init; }
    }
}
