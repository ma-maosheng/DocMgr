using DocMgr.Models.SystemSettings;

namespace DocMgr.Models.Shared
{
    /// <summary>
    /// 将附件列表按分类装入签批 / 照片 / 证明 / 其他分区集合（办理壳四区绑定用）。
    /// 未命中签批/照片/证明的条目一律归入「其他」。
    /// </summary>
    public static class ApprovalAttachmentPartitionSupport
    {
        /// <summary>
        /// 清空目标集合后按分类装入。
        /// </summary>
        /// <param name="source">源附件。</param>
        /// <param name="all">可选总表。</param>
        /// <param name="signed">签批单/签批交接单。</param>
        /// <param name="photos">实物/现场/硬盘照片；可为 null 表示本业务无照片区。</param>
        /// <param name="proof">证明材料；可为 null。</param>
        /// <param name="other">其他附件。</param>
        /// <param name="signedCategory">签批分类精确匹配。</param>
        /// <param name="isPhotoCategory">照片分类判定；null 表示无照片区。</param>
        /// <param name="proofCategory">证明分类精确匹配；null 表示无证明区。</param>
        public static void ClearAndPartition(
            IEnumerable<SystemAttachment> source,
            ICollection<SystemAttachment>? all,
            ICollection<SystemAttachment> signed,
            ICollection<SystemAttachment>? photos,
            ICollection<SystemAttachment>? proof,
            ICollection<SystemAttachment> other,
            string signedCategory,
            Func<string, bool>? isPhotoCategory = null,
            string? proofCategory = null)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(signed);
            ArgumentNullException.ThrowIfNull(other);
            ArgumentException.ThrowIfNullOrWhiteSpace(signedCategory);

            all?.Clear();
            signed.Clear();
            photos?.Clear();
            proof?.Clear();
            other.Clear();

            string signedKey = signedCategory.Trim();
            string? proofKey = string.IsNullOrWhiteSpace(proofCategory) ? null : proofCategory.Trim();

            foreach (SystemAttachment item in source)
            {
                all?.Add(item);
                string category = item.FileCategory?.Trim() ?? string.Empty;
                if (string.Equals(category, signedKey, StringComparison.Ordinal))
                {
                    signed.Add(item);
                }
                else if (isPhotoCategory != null && isPhotoCategory(category))
                {
                    photos?.Add(item);
                }
                else if (proofKey != null
                         && string.Equals(category, proofKey, StringComparison.Ordinal))
                {
                    proof?.Add(item);
                }
                else
                {
                    other.Add(item);
                }
            }
        }
    }
}
