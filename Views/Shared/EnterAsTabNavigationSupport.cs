using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace DocMgr.Views.Shared
{
    /// <summary>
    /// 在单行文本框 / 密码框上，将回车键按 Tab 处理，焦点移到下一可聚焦控件。
    /// </summary>
    public static class EnterAsTabNavigationSupport
    {
        /// <summary>
        /// 为根元素挂载 PreviewKeyDown：回车在输入框上等同于 Tab。
        /// </summary>
        public static void Attach(UIElement root)
        {
            ArgumentNullException.ThrowIfNull(root);
            root.PreviewKeyDown += OnPreviewKeyDown;
        }

        private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter || Keyboard.Modifiers != ModifierKeys.None)
            {
                return;
            }

            if (Keyboard.FocusedElement is not UIElement focused)
            {
                return;
            }

            // 按钮上保留回车激活；多行文本框保留换行。
            if (focused is ButtonBase)
            {
                return;
            }

            if (focused is TextBox { AcceptsReturn: true })
            {
                return;
            }

            if (focused is not TextBox and not PasswordBox)
            {
                return;
            }

            e.Handled = true;
            focused.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        }
    }
}
