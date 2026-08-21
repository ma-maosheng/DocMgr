using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using DocMgr.Services.Interfaces;
using DocMgr.ViewModels.Shared;
using DocMgr.Views.Shared;

namespace DocMgr.Services.Shared
{
    /// <summary>
    /// 在调用方父窗口上覆盖进度条：优先装饰层，否则加入现有面板。不改动 Window.Content。
    /// </summary>
    internal sealed class OperationProgressSession : IOperationProgressSession
    {
        private readonly Action _detach;
        private readonly OperationProgressDialogViewModel _viewModel;
        private readonly Dispatcher _dispatcher;
        private bool _disposed;

        private OperationProgressSession(
            Action detach,
            OperationProgressDialogViewModel viewModel,
            Dispatcher dispatcher)
        {
            _detach = detach;
            _viewModel = viewModel;
            _dispatcher = dispatcher;
        }

        public void SetStatus(string status)
        {
            Dispatch(() =>
            {
                if (!string.IsNullOrWhiteSpace(status))
                {
                    _viewModel.StatusText = status.Trim();
                }
            });
        }

        public void SetIndeterminate(string? status = null)
        {
            Dispatch(() => _viewModel.ApplyIndeterminate(status));
        }

        public void Report(int current, int total, string? status = null)
        {
            Dispatch(() => _viewModel.ApplyReport(current, total, status));
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Dispatch(_detach, pumpRender: false);
        }

        private void Dispatch(Action action, bool pumpRender = true)
        {
            void Apply()
            {
                action();
                if (pumpRender)
                {
                    PumpUi(_dispatcher);
                }
            }

            if (_dispatcher.CheckAccess())
            {
                Apply();
                return;
            }

            _dispatcher.Invoke(Apply, DispatcherPriority.Send);
        }

        private static void PumpUi(Dispatcher dispatcher)
        {
            var frame = new DispatcherFrame();
            dispatcher.BeginInvoke(
                DispatcherPriority.Render,
                new DispatcherOperationCallback(static state =>
                {
                    ((DispatcherFrame)state!).Continue = false;
                    return null;
                }),
                frame);
            Dispatcher.PushFrame(frame);
        }

        public static IOperationProgressSession Attach(
            Window host,
            OperationProgressDialogViewModel viewModel)
        {
            var overlay = new OperationProgressOverlay
            {
                DataContext = viewModel
            };

            UIElement adorned = host.Content as UIElement ?? host;
            AdornerLayer? layer = AdornerLayer.GetAdornerLayer(adorned)
                ?? AdornerLayer.GetAdornerLayer(host);

            if (layer != null)
            {
                var adorner = new OverlayAdorner(adorned, overlay);
                layer.Add(adorner);
                host.UpdateLayout();
                PumpUi(host.Dispatcher);
                return new OperationProgressSession(
                    () => layer.Remove(adorner),
                    viewModel,
                    host.Dispatcher);
            }

            Panel panel = FindHostPanel(host.Content)
                ?? throw new InvalidOperationException("当前窗口无法覆盖进度条。");

            PlaceOverlayOnPanel(panel, overlay);
            host.UpdateLayout();
            PumpUi(host.Dispatcher);
            return new OperationProgressSession(
                () => panel.Children.Remove(overlay),
                viewModel,
                host.Dispatcher);
        }

        private static Panel? FindHostPanel(object? content)
        {
            return content switch
            {
                Grid grid => grid,
                Panel panel => panel,
                Border border => FindHostPanel(border.Child),
                ContentControl control => FindHostPanel(control.Content),
                Decorator decorator => FindHostPanel(decorator.Child),
                _ => null
            };
        }

        private static void PlaceOverlayOnPanel(Panel panel, UIElement overlay)
        {
            if (panel is Grid grid)
            {
                int rows = Math.Max(1, grid.RowDefinitions.Count);
                int cols = Math.Max(1, grid.ColumnDefinitions.Count);
                Grid.SetRow(overlay, 0);
                Grid.SetColumn(overlay, 0);
                Grid.SetRowSpan(overlay, rows);
                Grid.SetColumnSpan(overlay, cols);
            }

            Panel.SetZIndex(overlay, 10000);
            panel.Children.Add(overlay);
        }

        private sealed class OverlayAdorner : Adorner
        {
            private readonly VisualCollection _visuals;
            private readonly UIElement _child;

            public OverlayAdorner(UIElement adornedElement, UIElement child)
                : base(adornedElement)
            {
                _child = child;
                _visuals = new VisualCollection(this)
                {
                    child
                };
            }

            protected override int VisualChildrenCount => _visuals.Count;

            protected override Visual GetVisualChild(int index) => _visuals[index];

            protected override Size MeasureOverride(Size constraint)
            {
                _child.Measure(constraint);
                return constraint;
            }

            protected override Size ArrangeOverride(Size finalSize)
            {
                _child.Arrange(new Rect(finalSize));
                return finalSize;
            }
        }
    }
}
