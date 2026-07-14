using System;

namespace DocMgr.Models.ArchiveContainers
{
    /// <summary>
    /// 统一立档容器类型。
    /// </summary>
    public enum ArchiveContainerKind
    {
        /// <summary>
        /// 档案盒。
        /// </summary>
        ArchiveBox = 0,

        /// <summary>
        /// 电子介质袋（电子立档单元）。
        /// </summary>
        ElectronicBag = 1
    }

    /// <summary>
    /// 统一立档容器抽象。
    /// </summary>
    public interface IArchiveContainer
    {
        /// <summary>
        /// 主键。
        /// </summary>
        int Id { get; }

        /// <summary>
        /// 容器编号（档案盒编号或电子立档编号）。
        /// </summary>
        string ContainerCode { get; }

        /// <summary>
        /// 所属项目。
        /// </summary>
        string ProjectName { get; }

        /// <summary>
        /// 所属年度。
        /// </summary>
        string Year { get; }

        /// <summary>
        /// 归档人。
        /// </summary>
        string ArchivedBy { get; }

        /// <summary>
        /// 归档时间。
        /// </summary>
        DateTime ArchivedDate { get; }

        /// <summary>
        /// 备注。
        /// </summary>
        string Remarks { get; }

        /// <summary>
        /// 容器类型。
        /// </summary>
        ArchiveContainerKind ContainerKind { get; }
    }
}
