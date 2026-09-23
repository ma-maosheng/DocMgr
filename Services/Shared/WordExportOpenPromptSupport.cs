using System.Diagnostics;
using System.Windows;
using DocMgr.Services.Interfaces;

namespace DocMgr.Services.Shared
{
    /// <summary>
    /// Word 导出成功后询问是否用系统关联程序（通常为 Word）打开文件。
    /// </summary>
    public static class WordExportOpenPromptSupport
    {
        /// <summary>
        /// 提示已保存，并询问是否打开；确认后以 Shell 打开。
        /// </summary>
        public static void NotifySavedAndOfferOpen(string filePath, IDialogService dialogService)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            ArgumentNullException.ThrowIfNull(dialogService);

            string path = filePath.Trim();
            bool open = dialogService.ShowConfirm(
                $"Word 文档已保存：\n{path}\n\n是否用 Word 打开该文件？",
                "导出 Word");
            if (!open)
            {
                return;
            }

            TryOpen(path, error => dialogService.ShowError(error));
        }

        /// <summary>
        /// 无 <see cref="IDialogService"/> 时使用 MessageBox（打印预览窗）。
        /// </summary>
        public static void NotifySavedAndOfferOpen(string filePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

            string path = filePath.Trim();
            var result = MessageBox.Show(
                $"Word 文档已保存：\n{path}\n\n是否用 Word 打开该文件？",
                "导出 Word",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            TryOpen(path, error => MessageBox.Show(
                error,
                "导出 Word",
                MessageBoxButton.OK,
                MessageBoxImage.Error));
        }

        private static void TryOpen(string filePath, Action<string> showError)
        {
            try
            {
                Process.Start(new ProcessStartInfo(filePath)
                {
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                showError("无法打开 Word 文档：" + ex.Message);
            }
        }
    }
}
