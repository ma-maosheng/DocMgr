using System.Windows;
using DocMgr.Models.Shared;

namespace DocMgr.Views.Shared
{
    /// <summary>
    /// 非图像附件的操作选择对话框。
    /// </summary>
    public partial class AttachmentViewChoiceDialog : Window
    {
        public AttachmentViewChoiceDialog(string displayFileName)
        {
            InitializeComponent();
            MessageTextBlock.Text = $"附件：{displayFileName}\n\n• 用默认程序打开：调用系统关联程序查看\n• 另存为：保存到本地指定位置";
        }

        public SystemAttachmentViewAction? Result { get; private set; }

        private void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            Result = SystemAttachmentViewAction.OpenWithDefaultApp;
            DialogResult = true;
            Close();
        }

        private void SaveAsButton_Click(object sender, RoutedEventArgs e)
        {
            Result = SystemAttachmentViewAction.SaveAs;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Result = null;
            DialogResult = false;
            Close();
        }
    }
}
