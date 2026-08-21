using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace DocMgr.Views.Shared
{
    /// <summary>
    /// 操作进度提示窗：导入过程中不可关闭。不挂主窗 Owner，避免主窗禁用时进度无法刷新。
    /// </summary>
    public partial class OperationProgressDialog : Window
    {
        internal bool AllowClose { get; set; }

        public OperationProgressDialog()
        {
            InitializeComponent();
            PreviewKeyDown += OnPreviewKeyDown;
            Closing += OnClosing;
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape || e.Key == Key.System)
            {
                e.Handled = true;
            }
        }

        private void OnClosing(object? sender, CancelEventArgs e)
        {
            if (!AllowClose)
            {
                e.Cancel = true;
            }
        }
    }
}
