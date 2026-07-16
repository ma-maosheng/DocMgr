namespace DocMgr.Models.YearlyArchive

{

    public sealed class InteractiveItemPhysicalMoveRequest

    {

        public string MediaKind { get; set; } = ArchiveRegisterDomainValues.MediaKindSimulated;



        public int SourceBoxId { get; set; }



        public int SourceUnitId { get; set; }

        public int SourceMediumId { get; set; }

        public string TargetCabinetName { get; set; } = string.Empty;



        public string TargetFace { get; set; } = string.Empty;



        public int TargetRow { get; set; }



        public int TargetColumn { get; set; }



        public string Remarks { get; set; } = string.Empty;

    }



    public sealed class InteractiveItemRelocationSource

    {

        public string MediaKind { get; init; } = ArchiveRegisterDomainValues.MediaKindSimulated;



        public int SourceBoxId { get; init; }



        public int SourceUnitId { get; init; }

        public int SourceMediumId { get; init; }

        public string DisplayText { get; init; } = string.Empty;



        public string BoxSpecification { get; init; } = string.Empty;



        public string SourceDedicatedSlotCategoryName { get; init; } = string.Empty;



        public string SourceStorageLocation { get; init; } = string.Empty;



        public bool IsOpticalDiscMedia { get; init; }

    }

}

