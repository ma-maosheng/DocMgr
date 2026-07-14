using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocMgr.Models.Projects
{
    [Table("ProjectInfos")]
    public class ProjectInfo
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public string ProjectName { get; set; } = string.Empty;

        public string ProjectCode { get; set; } = string.Empty;

        public string ImplementYear { get; set; } = string.Empty;

        public string CapitalMgrDept { get; set; } = string.Empty; // 厅资金管理部门

        public string Remark { get; set; } = string.Empty;
    }
}
