using DocMgr.Models.Cabinets;

using DocMgr.ViewModels.Base;



namespace DocMgr.ViewModels.Cabinets

{

    public class CabinetArchiveBoxContentItemViewModel : ViewModelBase

    {

        public CabinetArchiveBoxContentItemViewModel(CabinetArchiveBoxContentDescriptor descriptor)

        {

            SourceType = descriptor.SourceType;

            CategoryText = descriptor.CategoryText;

            IdentifierText = descriptor.IdentifierText;

            TitleText = descriptor.TitleText;

            MaterialName = descriptor.MaterialName;

            ProjectYear = descriptor.ProjectYear;

            ProjectName = descriptor.ProjectName;

            ProvideUnit = descriptor.ProvideUnit;

            ItemType = descriptor.ItemType;

            ConfidentialLevel = descriptor.ConfidentialLevel;

            ApprovedCopyCount = descriptor.ApprovedCopyCount;

            Note = descriptor.Note;

            CarrierTypeText = string.IsNullOrWhiteSpace(descriptor.CarrierTypeText)
                ? descriptor.CategoryText
                : descriptor.CarrierTypeText;

            ApplicantName = descriptor.ApplicantName;

            ArchivePurpose = descriptor.ArchivePurpose;

            StoragePath = descriptor.StoragePath;

            FilingStoragePath = descriptor.FilingStoragePath;

            MaterialCategory = descriptor.MaterialCategory;

            SubCategory = descriptor.SubCategory;

            DataOrganizationForm = descriptor.DataOrganizationForm;

            DataSizeText = descriptor.DataSizeText;

            ContentEntryBreakdownText = descriptor.ContentEntryBreakdownText;

            ContainerCode = descriptor.ContainerCode;

            BoxSpecs = descriptor.BoxSpecs;

            MediumCode = descriptor.MediumCode;

            FiledBy = descriptor.FiledBy;

            ArchiveCopyRoleDisplay = descriptor.ArchiveCopyRoleDisplay;

            QuantityText = descriptor.QuantityText;

            DetailText = descriptor.DetailText;

            DateText = descriptor.DateText;

            IsYearlyArchiveMediaItem = descriptor.IsYearlyArchiveMediaItem;

            IsElectronicMedia = descriptor.IsElectronicMedia;

            FilingFactId = descriptor.FilingFactId;

            RegisterRecordId = descriptor.RegisterRecordId;

            RegisterMediaId = descriptor.RegisterMediaId;

            MediaItemId = descriptor.MediaItemId;

            MediaKind = descriptor.MediaKind;

            FiledCopyCount = descriptor.FiledCopyCount;

            CurrentInArchiveCopyCount = descriptor.CurrentInArchiveCopyCount;

            PendingReturnCopyCount = descriptor.PendingReturnCopyCount;

            NoReturnCopyCount = descriptor.NoReturnCopyCount;

            LostCopyCount = descriptor.LostCopyCount;

            InventoryLostCopyCount = descriptor.InventoryLostCopyCount;

            InventoryScrapCopyCount = descriptor.InventoryScrapCopyCount;

            ElectronicStockStatusText = descriptor.ElectronicStockStatusText;

            HasOccupationLock = descriptor.HasOccupationLock;

            OccupationLockDisplayText = string.IsNullOrWhiteSpace(descriptor.OccupationLockDisplayText)

                ? "—"

                : descriptor.OccupationLockDisplayText;

        }



        public string SourceType { get; }



        public string CategoryText { get; }



        public string IdentifierText { get; }



        public string TitleText { get; }



        public string MaterialName { get; }



        public string ProjectYear { get; }



        public string ProjectName { get; }



        public string ProvideUnit { get; }



        public string ItemType { get; }



        public string ConfidentialLevel { get; }



        public int ApprovedCopyCount { get; }



        public string Note { get; }



        public string CarrierTypeText { get; }



        public string ApplicantName { get; }



        public string ArchivePurpose { get; }



        public string StoragePath { get; }



        public string FilingStoragePath { get; }



        public string MaterialCategory { get; }



        public string SubCategory { get; }



        public string DataOrganizationForm { get; }



        public string DataSizeText { get; }



        public string ContentEntryBreakdownText { get; }



        public string ContainerCode { get; }



        public string BoxSpecs { get; }



        public string MediumCode { get; }



        public string FiledBy { get; }



        public string ArchiveCopyRoleDisplay { get; }



        public string QuantityText { get; }



        public string DetailText { get; }



        public string DateText { get; }



        public bool IsYearlyArchiveMediaItem { get; }



        public bool IsElectronicMedia { get; }



        public int FilingFactId { get; }



        public int RegisterRecordId { get; }



        public int RegisterMediaId { get; }



        public int MediaItemId { get; }



        public string MediaKind { get; }



        public int FiledCopyCount { get; }



        public int CurrentInArchiveCopyCount { get; }



        public int PendingReturnCopyCount { get; }



        public int NoReturnCopyCount { get; }



        public int LostCopyCount { get; }



        public int InventoryLostCopyCount { get; }



        public int InventoryScrapCopyCount { get; }



        public string ElectronicStockStatusText { get; }



        public bool HasOccupationLock { get; }



        public string OccupationLockDisplayText { get; }



        public string FiledCopyCountDisplay => IsElectronicMedia ? "1" : FiledCopyCount.ToString();



        public string CurrentInArchiveCopyCountDisplay => IsElectronicMedia ? "—" : CurrentInArchiveCopyCount.ToString();



        public string PendingReturnCopyCountDisplay => IsElectronicMedia ? "—" : PendingReturnCopyCount.ToString();



        public string NoReturnCopyCountDisplay => IsElectronicMedia ? "—" : NoReturnCopyCount.ToString();



        public string LostCopyCountDisplay => IsElectronicMedia ? "—" : LostCopyCount.ToString();



        public string InventoryLostCopyCountDisplay => IsElectronicMedia ? "—" : InventoryLostCopyCount.ToString();



        public string InventoryScrapCopyCountDisplay => IsElectronicMedia ? "—" : InventoryScrapCopyCount.ToString();



        public string ElectronicStatusDisplay =>
            string.IsNullOrWhiteSpace(ElectronicStockStatusText) ? "-" : ElectronicStockStatusText;



        public string ApprovedCopyCountDisplay =>

            !IsYearlyArchiveMediaItem

                ? string.Empty

                : IsElectronicMedia

                    ? "1"

                    : ApprovedCopyCount.ToString();

    }

}


