using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocMgr.Models.NetworkTransfer
{
    /// <summary>
    /// 生产网在网台账（入网办结或加工产出登记写入）。
    /// </summary>
    [Table("NetworkOnNetAssets")]
    public sealed class NetworkOnNetAsset
    {
        [Key]
        public int Id { get; set; }

        /// <summary>在网资产编号。</summary>
        [Required]
        public string AssetNo { get; set; } = string.Empty;

        public string AssetKind { get; set; } = string.Empty;

        public string AssetName { get; set; } = string.Empty;

        public string ProjectName { get; set; } = string.Empty;

        public string Year { get; set; } = string.Empty;

        public string ServerPath { get; set; } = string.Empty;

        public string ConfidentialLevel { get; set; } = string.Empty;

        public string DataSizeText { get; set; } = string.Empty;

        public string VersionText { get; set; } = string.Empty;

        /// <summary>入网产生 / 加工产出。</summary>
        public string OriginKind { get; set; } = string.Empty;

        public int? OriginInboundItemId { get; set; }

        /// <summary>来源出网明细 Id（出网办结写入台账）。</summary>
        public int? OriginOutboundItemId { get; set; }

        public int? ParentAssetId { get; set; }

        public int? SourceFilingFactId { get; set; }

        public string LifecycleStatus { get; set; } = NetworkTransferDomainValues.LifecycleOnNet;

        public string Remark { get; set; } = string.Empty;

        public string RegisteredBy { get; set; } = string.Empty;

        public DateTime RegisteredAt { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        [NotMapped]
        public bool CanOutbound =>
            NetworkTransferDomainValues.CanOutbound(OriginKind, LifecycleStatus);

        [NotMapped]
        public bool CanDispose =>
            NetworkTransferDomainValues.CanDispose(LifecycleStatus);

        /// <summary>来源入网/出网单资料相对路径（列表展示）。</summary>
        [NotMapped]
        public string MaterialPath { get; set; } = string.Empty;

        /// <summary>来源入网/出网单资料名称（列表展示）。</summary>
        [NotMapped]
        public string MaterialName { get; set; } = string.Empty;

        /// <summary>来源入网提供部门（列表展示）。</summary>
        [NotMapped]
        public string ProvideUnit { get; set; } = string.Empty;

        /// <summary>来源申请部门（列表展示）。</summary>
        [NotMapped]
        public string ApplicantDept { get; set; } = string.Empty;

        /// <summary>服务器路径所属部门（列表展示）。</summary>
        [NotMapped]
        public string DepartmentName { get; set; } = string.Empty;

        /// <summary>服务器物理地址（列表展示）。</summary>
        [NotMapped]
        public string PhysicalPath { get; set; } = string.Empty;

        /// <summary>完整存储地址：物理地址 · 服务器路径 · 资料相对路径。</summary>
        [NotMapped]
        public string FullStorageAddress { get; set; } = string.Empty;

        /// <summary>来源入网/出网申请单号（列表展示）。</summary>
        [NotMapped]
        public string ApplicationNo { get; set; } = string.Empty;

        /// <summary>数据组织形式：目录型（根下可混合文件与子目录） / 文件型（仅散文件）。</summary>
        [NotMapped]
        public string DataOrganizationForm { get; set; } = string.Empty;

        /// <summary>目录/文件明细个数（目录型可为混合明细）。</summary>
        [NotMapped]
        public string EntryCountDisplay { get; set; } = string.Empty;

        /// <summary>关联电子介质明细 Id，供查看目录/文件详情。</summary>
        [NotMapped]
        public int? ElectronicMediaItemId { get; set; }
    }
}
