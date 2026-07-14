using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using DocMgr.ViewModels.Cabinets;
using DocMgr.Models.Cabinets;

namespace DocMgr.Views.Cabinets.Behaviors
{
    public static class CabinetLayoutInteractionBehavior
    {
        private sealed class DragState
        {
            public bool IsDragging { get; set; }
            public bool IsResizing { get; set; }
            public Point ClickPosition { get; set; }
            public Point StartPosition { get; set; }
        }

        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), typeof(CabinetLayoutInteractionBehavior),
                new PropertyMetadata(false, OnIsEnabledChanged));

        public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);
        public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

        public static readonly DependencyProperty CoordinateTargetProperty =
            DependencyProperty.RegisterAttached("CoordinateTarget", typeof(IInputElement), typeof(CabinetLayoutInteractionBehavior), new PropertyMetadata(null));

        public static void SetCoordinateTarget(DependencyObject element, IInputElement value) => element.SetValue(CoordinateTargetProperty, value);
        public static IInputElement GetCoordinateTarget(DependencyObject element) => (IInputElement)element.GetValue(CoordinateTargetProperty);

        public static readonly DependencyProperty SelectCommandProperty =
            DependencyProperty.RegisterAttached("SelectCommand", typeof(ICommand), typeof(CabinetLayoutInteractionBehavior), new PropertyMetadata(null));

        public static void SetSelectCommand(DependencyObject element, ICommand value) => element.SetValue(SelectCommandProperty, value);
        public static ICommand GetSelectCommand(DependencyObject element) => (ICommand)element.GetValue(SelectCommandProperty);

        public static readonly DependencyProperty SaveCommandProperty =
            DependencyProperty.RegisterAttached("SaveCommand", typeof(ICommand), typeof(CabinetLayoutInteractionBehavior), new PropertyMetadata(null));

        public static void SetSaveCommand(DependencyObject element, ICommand value) => element.SetValue(SaveCommandProperty, value);
        public static ICommand GetSaveCommand(DependencyObject element) => (ICommand)element.GetValue(SaveCommandProperty);

        public static readonly DependencyProperty ClearSelectionCommandProperty =
            DependencyProperty.RegisterAttached("ClearSelectionCommand", typeof(ICommand), typeof(CabinetLayoutInteractionBehavior),
                new PropertyMetadata(null, OnClearSelectionCommandChanged));

        public static void SetClearSelectionCommand(DependencyObject element, ICommand value) => element.SetValue(ClearSelectionCommandProperty, value);
        public static ICommand GetClearSelectionCommand(DependencyObject element) => (ICommand)element.GetValue(ClearSelectionCommandProperty);

        private static readonly DependencyProperty DragStateProperty =
            DependencyProperty.RegisterAttached("DragState", typeof(DragState), typeof(CabinetLayoutInteractionBehavior), new PropertyMetadata(null));

        private static DragState GetDragState(DependencyObject element)
        {
            var state = (DragState)element.GetValue(DragStateProperty);
            if (state == null)
            {
                state = new DragState();
                element.SetValue(DragStateProperty, state);
            }
            return state;
        }

        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not Border border) return;

            if ((bool)e.NewValue)
            {
                border.MouseLeftButtonDown += Cabinet_MouseLeftButtonDown;
                border.MouseMove += Cabinet_MouseMove;
                border.MouseLeftButtonUp += Cabinet_MouseLeftButtonUp;

                border.AddHandler(Thumb.DragDeltaEvent, new DragDeltaEventHandler(ResizeThumb_DragDelta), true);
                border.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(ResizeThumb_DragCompleted), true);
            }
            else
            {
                border.MouseLeftButtonDown -= Cabinet_MouseLeftButtonDown;
                border.MouseMove -= Cabinet_MouseMove;
                border.MouseLeftButtonUp -= Cabinet_MouseLeftButtonUp;

                border.RemoveHandler(Thumb.DragDeltaEvent, new DragDeltaEventHandler(ResizeThumb_DragDelta));
                border.RemoveHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(ResizeThumb_DragCompleted));
            }
        }

        private static void OnClearSelectionCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not UIElement element) return;

            element.MouseLeftButtonDown -= Background_MouseLeftButtonDown;
            if (e.NewValue is ICommand)
            {
                element.MouseLeftButtonDown += Background_MouseLeftButtonDown;
            }
        }

        private static void Background_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement fe) return;

            var command = GetClearSelectionCommand(fe);
            if (command == null) return;

            if (command.CanExecute(null))
            {
                command.Execute(null);
            }
        }

        private static void Cabinet_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Border border) return;
            if (FindAncestor<Thumb>(e.OriginalSource as DependencyObject) != null) return;

            if (border.DataContext is not Cabinet cabinet) return;

            var state = GetDragState(border);
            var target = GetCoordinateTarget(border) ?? border;

            var selectCommand = GetSelectCommand(border);
            if (selectCommand?.CanExecute(cabinet) == true)
            {
                selectCommand.Execute(cabinet);
            }

            if (e.ClickCount == 2)
            {
                if (border.Tag is ICabinetLayoutInteractionHost host
                    && host.AllowOpenOnDoubleClick
                    && host.OpenCabinetCommand?.CanExecute(cabinet) == true)
                {
                    host.OpenCabinetCommand.Execute(cabinet);
                }

                e.Handled = true;
                return;
            }

            if (border.Tag is ICabinetLayoutInteractionHost interactionHost && !interactionHost.AllowLayoutEdit)
            {
                e.Handled = true;
                return;
            }

            if (cabinet.Type == CabinetType.Standard)
            {
                e.Handled = true;
                return;
            }

            state.IsDragging = true;
            state.ClickPosition = e.GetPosition(target);
            state.StartPosition = new Point(cabinet.CanvasLeft, cabinet.CanvasTop);

            border.CaptureMouse();
            e.Handled = true;
        }

        private static void Cabinet_MouseMove(object sender, MouseEventArgs e)
        {
            if (sender is not Border border) return;
            if (border.DataContext is not Cabinet cabinet) return;
            if (border.Tag is ICabinetLayoutInteractionHost moveHost && !moveHost.AllowLayoutEdit) return;
            if (cabinet.Type == CabinetType.Standard) return;

            var state = GetDragState(border);
            if (!state.IsDragging || state.IsResizing) return;

            var target = GetCoordinateTarget(border) ?? border;
            var currentPosition = e.GetPosition(target);
            var offset = currentPosition - state.ClickPosition;

            cabinet.CanvasLeft = state.StartPosition.X + offset.X;
            cabinet.CanvasTop = state.StartPosition.Y + offset.Y;
        }

        private static void Cabinet_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Border border) return;
            if (border.DataContext is not Cabinet cabinet) return;
            if (border.Tag is ICabinetLayoutInteractionHost releaseHost && !releaseHost.AllowLayoutEdit) return;
            if (cabinet.Type == CabinetType.Standard) return;

            var state = GetDragState(border);
            if (!state.IsDragging) return;

            state.IsDragging = false;
            border.ReleaseMouseCapture();

            var saveCommand = GetSaveCommand(border);
            if (saveCommand?.CanExecute(cabinet) == true)
            {
                saveCommand.Execute(cabinet);
            }
        }

        private static void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (sender is not Border border) return;
            if (e.OriginalSource is not Thumb) return;
            if (border.DataContext is not Cabinet cabinet) return;
            if (border.Tag is ICabinetLayoutInteractionHost resizeHost && !resizeHost.AllowLayoutEdit)
            {
                e.Handled = true;
                return;
            }

            if (cabinet.Type == CabinetType.Standard)
            {
                e.Handled = true;
                return;
            }

            var state = GetDragState(border);
            state.IsResizing = true;

            double newWidth = Math.Max(30, cabinet.Width + e.HorizontalChange);
            double newHeight = Math.Max(30, cabinet.Height + e.VerticalChange);

            cabinet.Width = newWidth;
            cabinet.Height = newHeight;

            e.Handled = true;
        }

        private static void ResizeThumb_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            if (sender is not Border border) return;
            if (e.OriginalSource is not Thumb) return;
            if (border.DataContext is not Cabinet cabinet) return;
            if (border.Tag is ICabinetLayoutInteractionHost resizeCompleteHost && !resizeCompleteHost.AllowLayoutEdit)
            {
                e.Handled = true;
                return;
            }

            if (cabinet.Type == CabinetType.Standard)
            {
                e.Handled = true;
                return;
            }

            var state = GetDragState(border);
            state.IsResizing = false;

            var saveCommand = GetSaveCommand(border);
            if (saveCommand?.CanExecute(cabinet) == true)
            {
                saveCommand.Execute(cabinet);
            }

            e.Handled = true;
        }

        private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T t) return t;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }
    }
}