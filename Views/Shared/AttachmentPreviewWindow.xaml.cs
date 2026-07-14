using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using DocMgr.Models.Shared;
using DocMgr.Models.SystemSettings;
using Microsoft.Win32;

namespace DocMgr.Views.Shared
{
    /// <summary>
    /// 系统附件图像预览窗口。
    /// </summary>
    public partial class AttachmentPreviewWindow : Window
    {
        private readonly SystemAttachment _attachment;

        public AttachmentPreviewWindow(SystemAttachment attachment, BitmapImage imageSource)
        {
            ArgumentNullException.ThrowIfNull(attachment);
            ArgumentNullException.ThrowIfNull(imageSource);

            _attachment = attachment;
            InitializeComponent();

            string displayFileName = SystemAttachmentViewSupport.ResolveDisplayFileName(attachment);
            Title = $"附件预览 · {displayFileName}";
            TitleTextBlock.Text = displayFileName;
            PreviewImage.Source = imageSource;
        }

        private void OpenWithDefaultAppButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SystemAttachmentViewSupport.OpenWithDefaultApplication(_attachment);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"打开附件失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveAsButton_Click(object sender, RoutedEventArgs e)
        {
            SaveAttachmentAs();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void SaveAttachmentAs()
        {
            if (_attachment.FileContent == null || _attachment.FileContent.Length == 0)
            {
                MessageBox.Show(this, "附件内容为空，无法另存。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "另存附件",
                FileName = SystemAttachmentViewSupport.ResolveDisplayFileName(_attachment),
                Filter = "所有文件|*.*"
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            try
            {
                File.WriteAllBytes(dialog.FileName, _attachment.FileContent);
                MessageBox.Show(this, "附件已保存。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"保存附件失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
