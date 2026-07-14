using System;
using System.ComponentModel.DataAnnotations.Schema; // 引用 NotMapped

namespace DocMgr.Models.SystemSettings
{
    // 通用系统附件表
    public class SystemAttachment
    {
        public int Id { get; set; }

        // === 业务关联 ===
        // 业务类型标识，例如 "YearlyArchiveRegister", "ArchiveBorrow", "HistoryTopo"
        public string BusinessType { get; set; } = string.Empty;

        public string BusinessNo { get; set; } = string.Empty;   // 业务编号 (e.g. 表单号)
        public int BusinessId { get; set; }       // 关联的主键ID (方便程序内Join)

        // === 文件信息 ===
        public string FileName { get; set; } = string.Empty;      // 原始文件名
        public string Extension { get; set; } = string.Empty;     // 后缀名
        public long FileSize { get; set; }        // 字节数
        public byte[]? FileContent { get; set; }   // 二进制内容 (Blob)

        // === 审计与分类 ===
        // 附件分类 (e.g. "审批单扫描件", "电子原稿", "红头文件")
        public string FileCategory { get; set; } = "一般附件";
        public DateTime UploadTime { get; set; }
        public string UploaderName { get; set; } = string.Empty;

        // 新增：不映射到数据库，仅用于显示
        [NotMapped]
        public string FileSizeStr => (FileSize / 1024.0).ToString("F1");
    }
}