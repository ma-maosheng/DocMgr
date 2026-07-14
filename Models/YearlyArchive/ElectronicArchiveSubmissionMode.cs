namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 电子介质立档提交模式。
    /// </summary>
    public enum ElectronicArchiveSubmissionMode
    {
        /// <summary>
        /// 拷贝型场景，新建硬盘袋立档。
        /// </summary>
        CopyNewHardDisk,

        /// <summary>
        /// 拷贝型场景，新建单张光盘袋立档。
        /// </summary>
        CopyNewOpticalDisc,

        /// <summary>
        /// 拷贝型场景，并入本项目既有硬盘袋。
        /// </summary>
        CopyAppendExistingHardDisk,

        /// <summary>
        /// 光盘留存场景，单张光盘独立新建立档。
        /// </summary>
        RetainedOpticalDiscSingleNew,

        /// <summary>
        /// 硬盘留存场景，直接使用原硬盘新建立档。
        /// </summary>
        RetainedHardDiskDirectNew,

        /// <summary>
        /// 硬盘留存场景，拷贝到单张光盘后新建立档。
        /// </summary>
        RetainedHardDiskCopyToOpticalDisc,

        /// <summary>
        /// 硬盘留存场景，并入本项目既有硬盘袋。
        /// </summary>
        RetainedHardDiskAppendExistingHardDisk
    }
}
