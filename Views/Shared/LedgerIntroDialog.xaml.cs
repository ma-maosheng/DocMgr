using System.Windows;
using DocMgr.Models.Shared;

namespace DocMgr.Views.Shared
{
    /// <summary>
    /// 台账简介：业务范围、关联对象、写入逻辑。
    /// </summary>
    public partial class LedgerIntroDialog : Window
    {
        public LedgerIntroDialog(LedgerIntroCopySupport.Content content)
        {
            ArgumentNullException.ThrowIfNull(content);
            InitializeComponent();
            DialogTitle = content.Title;
            ScopeText = content.Scope;
            RelatedObjectsText = content.RelatedObjects;
            WriteLogicText = content.WriteLogic;
            DataContext = this;
        }

        public string DialogTitle { get; }

        public string ScopeText { get; }

        public string RelatedObjectsText { get; }

        public string WriteLogicText { get; }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}
