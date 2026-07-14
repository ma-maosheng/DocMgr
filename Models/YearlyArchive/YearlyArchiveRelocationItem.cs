using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocMgr.Models.YearlyArchive
{
    [Table("YearlyArchiveRelocationItems")]
    public class YearlyArchiveRelocationItem
    {
        [Key]
        public int Id { get; set; }

        public int RelocationRecordId { get; set; }

        public int FilingFactId { get; set; }

        public int SourceLinkId { get; set; }

        public string SourceLinkType { get; set; } = string.Empty;

        public string BeforeContainerCode { get; set; } = string.Empty;

        public string BeforeStorageLocation { get; set; } = string.Empty;

        public string AfterContainerCode { get; set; } = string.Empty;

        public string AfterStorageLocation { get; set; } = string.Empty;

        public virtual YearlyArchiveRelocationRecord RelocationRecord { get; set; } = null!;
    }
}
