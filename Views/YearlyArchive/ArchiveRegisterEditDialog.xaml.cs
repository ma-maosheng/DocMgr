using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using DocMgr.ViewModels.YearlyArchive;

namespace DocMgr.Views.YearlyArchive
{
    public partial class ArchiveRegisterEditDialog : Window
    {
        private sealed class DialogUiState
        {
            public double HorizontalOffset { get; set; }
            public double VerticalOffset { get; set; }
            public List<int> FocusVisualPath { get; set; } = new();
        }

        private static readonly Dictionary<string, DialogUiState> UiStateByRecord = new();

        private bool _restoredForCurrentSession;

        public ArchiveRegisterEditDialog()
        {
            InitializeComponent();
            Loaded += ArchiveRegisterEditDialog_Loaded;
            ContentRendered += ArchiveRegisterEditDialog_ContentRendered;
            Closing += ArchiveRegisterEditDialog_Closing;
        }

        private void ArchiveRegisterEditDialog_Loaded(object sender, RoutedEventArgs e)
        {
            TryRestoreUiState();
        }

        private void ArchiveRegisterEditDialog_ContentRendered(object? sender, EventArgs e)
        {
            TryRestoreUiState();
        }

        private void ArchiveRegisterEditDialog_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveUiState();
        }

        private void TryRestoreUiState()
        {
            if (_restoredForCurrentSession)
            {
                return;
            }

            if (!TryGetStateKey(out string key))
            {
                return;
            }

            if (!UiStateByRecord.TryGetValue(key, out DialogUiState? state))
            {
                return;
            }

            _restoredForCurrentSession = true;
            ApplyUiState(state);
        }

        private void ApplyUiState(DialogUiState state)
        {
            FormScrollViewer.ScrollToHorizontalOffset(state.HorizontalOffset);
            FormScrollViewer.ScrollToVerticalOffset(state.VerticalOffset);

            // 等待布局稳定后再次定位，避免 DataTemplate 延迟渲染造成偏移丢失。
            Dispatcher.BeginInvoke(() =>
            {
                FormScrollViewer.ScrollToHorizontalOffset(state.HorizontalOffset);
                FormScrollViewer.ScrollToVerticalOffset(state.VerticalOffset);
                RestoreFocus(state.FocusVisualPath);
            }, DispatcherPriority.Loaded);
        }

        private void SaveUiState()
        {
            if (!TryGetStateKey(out string key))
            {
                return;
            }

            var state = new DialogUiState
            {
                HorizontalOffset = FormScrollViewer.HorizontalOffset,
                VerticalOffset = FormScrollViewer.VerticalOffset,
                FocusVisualPath = CaptureFocusPath()
            };

            UiStateByRecord[key] = state;
        }

        private bool TryGetStateKey(out string key)
        {
            key = string.Empty;
            if (DataContext is not ArchiveRegisterViewModel vm)
            {
                return false;
            }

            int recordId = vm.CurrentRecord?.Id ?? 0;
            if (recordId <= 0)
            {
                return false;
            }

            key = $"{vm.WorkspaceMode}:{recordId}";
            return true;
        }

        private List<int> CaptureFocusPath()
        {
            if (Keyboard.FocusedElement is not DependencyObject focused)
            {
                return new List<int>();
            }

            if (!IsDescendantOfWindow(focused))
            {
                return new List<int>();
            }

            var pathFromRoot = new List<int>();
            DependencyObject? current = focused;
            while (current != null && current != this)
            {
                DependencyObject? parent = VisualTreeHelper.GetParent(current);
                if (parent == null)
                {
                    break;
                }

                int index = GetChildIndex(parent, current);
                if (index < 0)
                {
                    break;
                }

                pathFromRoot.Add(index);
                current = parent;
            }

            pathFromRoot.Reverse();
            return pathFromRoot;
        }

        private void RestoreFocus(IReadOnlyList<int> pathFromRoot)
        {
            if (pathFromRoot.Count == 0)
            {
                return;
            }

            DependencyObject current = this;
            foreach (int index in pathFromRoot)
            {
                int childrenCount = VisualTreeHelper.GetChildrenCount(current);
                if (index < 0 || index >= childrenCount)
                {
                    return;
                }

                current = VisualTreeHelper.GetChild(current, index);
            }

            if (current is UIElement element && element.Focusable && element.IsVisible)
            {
                Keyboard.Focus(element);
            }
        }

        private bool IsDescendantOfWindow(DependencyObject node)
        {
            DependencyObject? current = node;
            while (current != null)
            {
                if (ReferenceEquals(current, this))
                {
                    return true;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return false;
        }

        private static int GetChildIndex(DependencyObject parent, DependencyObject child)
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                if (ReferenceEquals(VisualTreeHelper.GetChild(parent, i), child))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
