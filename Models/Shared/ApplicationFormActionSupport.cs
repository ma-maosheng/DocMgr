namespace DocMgr.Models.Shared
{
    /// <summary>
    /// 申请页面「保存草稿 / 提交申请 / 打印申请」按钮可用状态统一规则。
    /// </summary>
    public static class ApplicationFormActionSupport
    {
        /// <summary>
        /// 三种申请按钮的可用状态。
        /// </summary>
        public readonly struct ActionState
        {
            public ActionState(bool canSaveDraft, bool canSubmitApplication, bool canPrintApplication)
            {
                CanSaveDraft = canSaveDraft;
                CanSubmitApplication = canSubmitApplication;
                CanPrintApplication = canPrintApplication;
            }

            /// <summary>是否可保存草稿。</summary>
            public bool CanSaveDraft { get; }

            /// <summary>是否可提交申请。</summary>
            public bool CanSubmitApplication { get; }

            /// <summary>是否可打印申请。</summary>
            public bool CanPrintApplication { get; }
        }

        /// <summary>
        /// 解析申请按钮可用状态：未保存草稿前仅可保存；保存草稿后可保存/提交；已提交后仅可打印。
        /// </summary>
        /// <param name="recordId">已持久化记录 Id；未保存时为 0。</param>
        /// <param name="isDraft">是否为草稿（未提交）状态。</param>
        public static ActionState Resolve(int recordId, bool isDraft)
        {
            if (!isDraft)
            {
                return new ActionState(false, false, true);
            }

            if (recordId <= 0)
            {
                return new ActionState(true, false, false);
            }

            return new ActionState(true, true, false);
        }
    }
}
