using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DocMgr.Models.Cabinets;
using DocMgr.ViewModels.Cabinets;

namespace DocMgr.Views.Cabinets
{
    public partial class CabinetOpenDialog : Window
    {
        private Point? _interactiveDragStartPoint;
        private bool _interactiveDragInProgress;

        public CabinetOpenDialog()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is CabinetOpenViewModel { IsSingleSlotSnapshot: true } viewModel)
            {
                CabinetPreviewLayer.Visibility = Visibility.Collapsed;
                CabinetPreviewLayer.Opacity = 0;
                SlotsHost.Opacity = 1;
                SlotsScaleTransform.ScaleX = 1;
                SlotsScaleTransform.ScaleY = 1;
                SlotsTranslateTransform.Y = 0;
                SlotsHost.Margin = new Thickness(8);
                SlotsHost.Padding = new Thickness(8);
                Dispatcher.BeginInvoke(ApplySnapshotViewportSize, System.Windows.Threading.DispatcherPriority.Loaded);
                return;
            }

            PlayCabinetOpenAnimation(GetCabinetType());
        }

        private void SlotContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu { PlacementTarget: FrameworkElement { DataContext: CabinetSlotViewModel slot } } &&
                DataContext is CabinetOpenViewModel viewModel)
            {
                viewModel.PrepareCompactSlotContextMenu(slot);
            }

            SetSlotContextMenuState(sender, true);
            CommandManager.InvalidateRequerySuggested();
        }

        private void SlotContextMenu_Closed(object sender, RoutedEventArgs e)
        {
            SetSlotContextMenuState(sender, false);
        }

        private void ArchiveBoxContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            SetArchiveBoxContextMenuState(sender, true);
        }

        private void ArchiveBoxContextMenu_Closed(object sender, RoutedEventArgs e)
        {
            SetArchiveBoxContextMenuState(sender, false);
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (DataContext is CabinetOpenViewModel { IsSingleSlotSnapshot: true })
            {
                ApplySnapshotViewportSize();
            }
        }

        private void ApplySnapshotViewportSize()
        {
            if (DataContext is not CabinetOpenViewModel { IsSingleSlotSnapshot: true } viewModel)
            {
                return;
            }

            const double chromePadding = 8d;
            double width = Math.Max(SlotsHost.ActualWidth - chromePadding, 240d);
            double height = Math.Max(SlotsHost.ActualHeight - chromePadding, 180d);
            if (width <= 0d || height <= 0d)
            {
                return;
            }

            viewModel.UpdateSingleSlotSnapshotDimensions(width, height);
        }

        private void SlotsHost_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (DataContext is not CabinetOpenViewModel viewModel)
            {
                return;
            }

            if (!viewModel.IsSingleSlotSnapshot)
            {
                if (!viewModel.IsMagneticDiskCabinet)
                {
                    return;
                }

                const double chromePadding = 36d;
                viewModel.UpdateMagneticDiskSlotDimensions(
                    Math.Max(e.NewSize.Width - chromePadding, 240d),
                    Math.Max(e.NewSize.Height - chromePadding, 180d));
                return;
            }

            ApplySnapshotViewportSize();
        }

        private void Slot_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement { DataContext: CabinetSlotViewModel slot })
            {
                return;
            }

            if (DataContext is not CabinetOpenViewModel viewModel)
            {
                return;
            }

            if (e.ClickCount >= 2)
            {
                if (!IsInteractiveSlotContent(e.OriginalSource as DependencyObject))
                {
                    viewModel.ShowSlotDetail(slot);
                    e.Handled = true;
                }

                return;
            }

            if (!viewModel.IsCompactDisplayMode)
            {
                return;
            }

            bool ctrlPressed = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
            bool shiftPressed = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
            viewModel.HandleCompactSlotSelection(slot, ctrlPressed, shiftPressed);
            e.Handled = true;
        }

        private static bool IsInteractiveSlotContent(DependencyObject? source)
        {
            while (source != null)
            {
                if (source is FrameworkElement { DataContext: ArchiveBoxItemViewModel or CabinetHardDiskMediumItemViewModel })
                {
                    return true;
                }

                source = System.Windows.Media.VisualTreeHelper.GetParent(source);
            }

            return false;
        }

        private void ArchiveBox_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement { DataContext: ArchiveBoxItemViewModel archiveBox })
            {
                return;
            }

            if (DataContext is CabinetOpenViewModel viewModel)
            {
                viewModel.SelectArchiveBox(archiveBox);
            }

            e.Handled = true;
        }

        private void HardDiskMedium_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement { DataContext: CabinetHardDiskMediumItemViewModel medium })
            {
                return;
            }

            if (DataContext is CabinetOpenViewModel viewModel)
            {
                viewModel.SelectHardDiskMedium(medium);
            }

            e.Handled = true;
        }

        private void InteractiveItemDrag_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not CabinetOpenViewModel viewModel || !viewModel.SupportsInteractiveItemRelocationDrag)
            {
                ResetInteractiveDragTracking();
                return;
            }

            if (sender is not FrameworkElement element)
            {
                ResetInteractiveDragTracking();
                return;
            }

            if (element.DataContext is ArchiveBoxItemViewModel archiveBox)
            {
                if (viewModel.TryCreateDragPayloadFromArchiveBox(archiveBox) == null)
                {
                    ResetInteractiveDragTracking();
                    return;
                }
            }
            else if (element.DataContext is CabinetHardDiskMediumItemViewModel medium)
            {
                if (viewModel.TryCreateDragPayloadFromMedium(medium) == null)
                {
                    ResetInteractiveDragTracking();
                    return;
                }
            }
            else
            {
                ResetInteractiveDragTracking();
                return;
            }

            _interactiveDragStartPoint = e.GetPosition(null);
        }

        private void InteractiveItemDrag_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_interactiveDragInProgress || _interactiveDragStartPoint == null)
            {
                return;
            }

            if (e.LeftButton != MouseButtonState.Pressed)
            {
                ResetInteractiveDragTracking();
                return;
            }

            Point currentPosition = e.GetPosition(null);
            Vector dragDistance = _interactiveDragStartPoint.Value - currentPosition;
            if (Math.Abs(dragDistance.X) < SystemParameters.MinimumHorizontalDragDistance
                && Math.Abs(dragDistance.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            if (DataContext is not CabinetOpenViewModel viewModel || sender is not DependencyObject dragSource)
            {
                ResetInteractiveDragTracking();
                return;
            }

            InteractiveItemRelocationDragPayload? payload = null;
            if (sender is FrameworkElement { DataContext: ArchiveBoxItemViewModel archiveBox })
            {
                payload = viewModel.TryCreateDragPayloadFromArchiveBox(archiveBox);
            }
            else if (sender is FrameworkElement { DataContext: CabinetHardDiskMediumItemViewModel medium })
            {
                payload = viewModel.TryCreateDragPayloadFromMedium(medium);
            }

            if (payload == null)
            {
                ResetInteractiveDragTracking();
                return;
            }

            _interactiveDragInProgress = true;
            var dataObject = new DataObject(InteractiveItemRelocationDragPayload.DataFormat, payload);
            try
            {
                DragDrop.DoDragDrop(dragSource, dataObject, DragDropEffects.Move);
            }
            finally
            {
                _interactiveDragInProgress = false;
                ResetInteractiveDragTracking();
                viewModel.ClearInteractiveItemDragHover();
            }
        }

        private void InteractiveItemDrag_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            ResetInteractiveDragTracking();
        }

        private void InteractiveItemSlot_DragOver(object sender, DragEventArgs e)
        {
            if (DataContext is not CabinetOpenViewModel viewModel || !viewModel.SupportsInteractiveItemRelocationDrag)
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            if (sender is not FrameworkElement { DataContext: CabinetSlotViewModel slot }
                || !TryGetInteractiveItemDragPayload(e, out InteractiveItemRelocationDragPayload? payload)
                || payload == null)
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                viewModel.ClearInteractiveItemDragHover();
                return;
            }

            bool canDrop = viewModel.CanAcceptInteractiveItemDragOnSlot(slot, payload);
            e.Effects = canDrop ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;
            viewModel.SetInteractiveItemDragHover(slot, payload);
        }

        private void InteractiveItemSlot_DragLeave(object sender, DragEventArgs e)
        {
            if (sender is FrameworkElement { DataContext: CabinetSlotViewModel slot }
                && DataContext is CabinetOpenViewModel viewModel)
            {
                slot.ClearInteractiveRelocationDropHighlight();
                viewModel.ClearInteractiveItemDragHover();
            }

            e.Handled = true;
        }

        private async void InteractiveItemSlot_Drop(object sender, DragEventArgs e)
        {
            if (DataContext is not CabinetOpenViewModel viewModel
                || sender is not FrameworkElement { DataContext: CabinetSlotViewModel slot }
                || !TryGetInteractiveItemDragPayload(e, out InteractiveItemRelocationDragPayload? payload)
                || payload == null)
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            e.Handled = true;
            viewModel.ClearInteractiveItemDragHover();
            await viewModel.HandleInteractiveItemDropAsync(slot, payload);
        }

        private static bool TryGetInteractiveItemDragPayload(DragEventArgs e, out InteractiveItemRelocationDragPayload? payload)
        {
            if (e.Data.GetDataPresent(InteractiveItemRelocationDragPayload.DataFormat))
            {
                payload = e.Data.GetData(InteractiveItemRelocationDragPayload.DataFormat) as InteractiveItemRelocationDragPayload;
                return payload != null;
            }

            payload = null;
            return false;
        }

        private void ResetInteractiveDragTracking()
        {
            _interactiveDragStartPoint = null;
        }

        private CabinetType GetCabinetType()
        {
            return DataContext is CabinetOpenViewModel viewModel
                ? viewModel.Request.CabinetType
                : CabinetType.Standard;
        }

        private static void SetSlotContextMenuState(object sender, bool isOpen)
        {
            if (sender is not ContextMenu { PlacementTarget: FrameworkElement { DataContext: CabinetSlotViewModel slotViewModel } })
            {
                return;
            }

            slotViewModel.IsContextMenuOpen = isOpen;
        }

        private static void SetArchiveBoxContextMenuState(object sender, bool isOpen)
        {
            if (sender is not ContextMenu { PlacementTarget: FrameworkElement { DataContext: ArchiveBoxItemViewModel archiveBoxViewModel } })
            {
                return;
            }

            archiveBoxViewModel.IsContextMenuOpen = isOpen;
        }

        private void PreparePreviewByCabinetType(CabinetType cabinetType)
        {
            StandardInteriorPanel.Visibility = cabinetType == CabinetType.Standard ? Visibility.Visible : Visibility.Collapsed;
            VerticalInteriorPanel.Visibility = cabinetType == CabinetType.Vertical ? Visibility.Visible : Visibility.Collapsed;
            HorizontalInteriorPanel.Visibility = cabinetType == CabinetType.Horizontal || cabinetType == CabinetType.MagneticDisk ? Visibility.Visible : Visibility.Collapsed;

            LeftDoorPanel.Visibility = cabinetType == CabinetType.Standard ? Visibility.Visible : Visibility.Collapsed;
            RightDoorPanel.Visibility = cabinetType == CabinetType.Standard ? Visibility.Visible : Visibility.Collapsed;
            PreviewCenterDivider.Visibility = cabinetType == CabinetType.Standard ? Visibility.Visible : Visibility.Collapsed;
            VerticalDoorPanel.Visibility = cabinetType == CabinetType.Vertical ? Visibility.Visible : Visibility.Collapsed;
            HorizontalDrawerPanel.Visibility = cabinetType == CabinetType.Horizontal || cabinetType == CabinetType.MagneticDisk ? Visibility.Visible : Visibility.Collapsed;

            switch (cabinetType)
            {
                case CabinetType.Vertical:
                    CabinetPreviewShell.Width = 280;
                    CabinetPreviewShell.Height = 300;
                    CabinetPreviewShell.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE0, 0xE7, 0xEF));
                    break;
                case CabinetType.Horizontal:
                    CabinetPreviewShell.Width = 400;
                    CabinetPreviewShell.Height = 180;
                    CabinetPreviewShell.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE6, 0xEC, 0xF3));
                    break;
                case CabinetType.MagneticDisk:
                    CabinetPreviewShell.Width = 360;
                    CabinetPreviewShell.Height = 260;
                    CabinetPreviewShell.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE8, 0xEA, 0xF6));
                    break;
                default:
                    CabinetPreviewShell.Width = 360;
                    CabinetPreviewShell.Height = 240;
                    CabinetPreviewShell.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE2, 0xE8, 0xF0));
                    break;
            }
        }
    }
}
