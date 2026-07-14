using System;
using System.Windows;
using System.Windows.Media.Animation;
using DocMgr.Models.Cabinets;

namespace DocMgr.Views.Cabinets
{
    public partial class CabinetOpenDialog
    {
        /// <summary>开柜过渡动画，总时长约 1.2 秒。</summary>
        private static readonly TimeSpan EstablishDuration = TimeSpan.FromMilliseconds(260);
        private static readonly TimeSpan BackdropFadeDuration = TimeSpan.FromMilliseconds(220);
        private static readonly TimeSpan CaptionEnterBegin = TimeSpan.FromMilliseconds(60);
        private static readonly TimeSpan CaptionEnterDuration = TimeSpan.FromMilliseconds(320);
        private static readonly TimeSpan DoorOpenBegin = TimeSpan.FromMilliseconds(120);
        private static readonly TimeSpan DoorOpenDuration = TimeSpan.FromMilliseconds(620);
        private static readonly TimeSpan SecondaryDoorDelay = TimeSpan.FromMilliseconds(70);
        private static readonly TimeSpan InteriorRevealBegin = TimeSpan.FromMilliseconds(220);
        private static readonly TimeSpan InteriorRevealDuration = TimeSpan.FromMilliseconds(520);
        private static readonly TimeSpan PreviewExitBegin = TimeSpan.FromMilliseconds(680);
        private static readonly TimeSpan PreviewExitDuration = TimeSpan.FromMilliseconds(360);
        private static readonly TimeSpan SlotsRevealBegin = TimeSpan.FromMilliseconds(720);
        private static readonly TimeSpan SlotsRevealDuration = TimeSpan.FromMilliseconds(480);
        private static readonly TimeSpan ProgressDuration = TimeSpan.FromMilliseconds(1200);
        private static readonly TimeSpan DividerFadeBegin = TimeSpan.FromMilliseconds(260);
        private static readonly TimeSpan DividerFadeDuration = TimeSpan.FromMilliseconds(280);

        private static readonly IEasingFunction EstablishEase = new QuadraticEase { EasingMode = EasingMode.EaseOut };
        private static readonly IEasingFunction DoorOpenEase = new CubicEase { EasingMode = EasingMode.EaseOut };
        private static readonly IEasingFunction DrawerPullEase = new CubicEase { EasingMode = EasingMode.EaseOut };
        private static readonly IEasingFunction SlideDoorEase = new CubicEase { EasingMode = EasingMode.EaseOut };
        private static readonly IEasingFunction InteriorRevealEase = new QuadraticEase { EasingMode = EasingMode.EaseOut };
        private static readonly IEasingFunction PreviewExitEase = new QuadraticEase { EasingMode = EasingMode.EaseIn };
        private static readonly IEasingFunction SlotsRevealEase = new CubicEase { EasingMode = EasingMode.EaseOut };
        private static readonly IEasingFunction ProgressEase = new QuadraticEase { EasingMode = EasingMode.EaseInOut };

        private void PlayCabinetOpenAnimation(CabinetType cabinetType)
        {
            PreparePreviewByCabinetType(cabinetType);
            ResetPreviewAnimationState(cabinetType);

            var storyboard = new Storyboard { FillBehavior = FillBehavior.Stop };
            AddEstablishAnimations(storyboard);
            AddCabinetTypeDoorAnimations(storyboard, cabinetType);
            AddInteriorRevealAnimations(storyboard, cabinetType);
            AddPreviewCaptionAnimations(storyboard);
            AddPreviewExitAnimations(storyboard);
            AddSlotsRevealAnimations(storyboard);
            AddProgressAnimations(storyboard);

            storyboard.Completed += (_, _) => CompleteCabinetOpenAnimation(cabinetType);
            storyboard.Begin();
        }

        private void ResetPreviewAnimationState(CabinetType cabinetType)
        {
            CabinetPreviewLayer.Visibility = Visibility.Visible;
            CabinetPreviewLayer.Opacity = 1;
            PreviewScaleTransform.ScaleX = 1;
            PreviewScaleTransform.ScaleY = 1;
            PreviewShellScaleTransform.ScaleX = 0.94;
            PreviewShellScaleTransform.ScaleY = 0.94;
            PreviewBackdrop.Opacity = 0;
            PreviewProgressScaleTransform.ScaleX = 0;
            PreviewCaptionPanel.Opacity = 0;
            PreviewCaptionTranslateTransform.Y = 8;
            ResetInteriorScale(StandardInteriorPanel);
            ResetInteriorScale(VerticalInteriorPanel);
            ResetInteriorScale(HorizontalInteriorPanel);

            SlotsHost.Opacity = 0;
            SlotsScaleTransform.ScaleX = 0.96;
            SlotsScaleTransform.ScaleY = 0.96;
            SlotsTranslateTransform.Y = 12;

            switch (cabinetType)
            {
                case CabinetType.Vertical:
                    VerticalDoorTranslateTransform.X = 0;
                    VerticalDoorPanel.Opacity = 1;
                    VerticalInteriorPanel.Opacity = 0.35;
                    break;
                case CabinetType.Horizontal:
                case CabinetType.MagneticDisk:
                    HorizontalDrawerTranslateTransform.Y = 0;
                    HorizontalDrawerPanel.Opacity = 1;
                    HorizontalInteriorPanel.Opacity = 0.35;
                    break;
                default:
                    LeftDoorRotateTransform.Angle = 0;
                    RightDoorRotateTransform.Angle = 0;
                    LeftDoorPanel.Opacity = 1;
                    RightDoorPanel.Opacity = 1;
                    PreviewCenterDivider.Opacity = 1;
                    StandardInteriorPanel.Opacity = 0.35;
                    break;
            }
        }

        private void AddEstablishAnimations(Storyboard storyboard)
        {
            storyboard.Children.Add(CreateDoubleAnimation(
                PreviewShellScaleTransform,
                "ScaleX",
                0.94,
                1,
                EstablishDuration,
                EstablishEase));

            storyboard.Children.Add(CreateDoubleAnimation(
                PreviewShellScaleTransform,
                "ScaleY",
                0.94,
                1,
                EstablishDuration,
                EstablishEase));

            storyboard.Children.Add(CreateDoubleAnimation(
                PreviewBackdrop,
                OpacityProperty,
                0,
                0.72,
                BackdropFadeDuration,
                EstablishEase,
                TimeSpan.FromMilliseconds(40)));
        }

        private void AddCabinetTypeDoorAnimations(Storyboard storyboard, CabinetType cabinetType)
        {
            switch (cabinetType)
            {
                case CabinetType.Vertical:
                    AddVerticalDoorAnimations(storyboard);
                    break;
                case CabinetType.Horizontal:
                case CabinetType.MagneticDisk:
                    AddHorizontalDrawerAnimations(storyboard);
                    break;
                default:
                    AddStandardDoorAnimations(storyboard);
                    break;
            }
        }

        private void AddStandardDoorAnimations(Storyboard storyboard)
        {
            storyboard.Children.Add(CreateDoubleAnimation(
                LeftDoorRotateTransform,
                "Angle",
                0,
                -102,
                DoorOpenDuration,
                DoorOpenEase,
                DoorOpenBegin));

            storyboard.Children.Add(CreateDoubleAnimation(
                RightDoorRotateTransform,
                "Angle",
                0,
                102,
                DoorOpenDuration,
                DoorOpenEase,
                DoorOpenBegin + SecondaryDoorDelay));

            storyboard.Children.Add(CreateDoubleAnimation(
                LeftDoorPanel,
                OpacityProperty,
                1,
                0.2,
                DoorOpenDuration,
                DoorOpenEase,
                DoorOpenBegin));

            storyboard.Children.Add(CreateDoubleAnimation(
                RightDoorPanel,
                OpacityProperty,
                1,
                0.2,
                DoorOpenDuration,
                DoorOpenEase,
                DoorOpenBegin + SecondaryDoorDelay));

            storyboard.Children.Add(CreateDoubleAnimation(
                PreviewCenterDivider,
                OpacityProperty,
                1,
                0,
                DividerFadeDuration,
                InteriorRevealEase,
                DividerFadeBegin));
        }

        private void AddVerticalDoorAnimations(Storyboard storyboard)
        {
            storyboard.Children.Add(CreateDoubleAnimation(
                VerticalDoorTranslateTransform,
                "X",
                0,
                162,
                DoorOpenDuration,
                SlideDoorEase,
                DoorOpenBegin));

            storyboard.Children.Add(CreateDoubleAnimation(
                VerticalDoorPanel,
                OpacityProperty,
                1,
                0.16,
                DoorOpenDuration,
                SlideDoorEase,
                DoorOpenBegin));
        }

        private void AddHorizontalDrawerAnimations(Storyboard storyboard)
        {
            storyboard.Children.Add(CreateDoubleAnimation(
                HorizontalDrawerTranslateTransform,
                "Y",
                0,
                124,
                DoorOpenDuration,
                DrawerPullEase,
                DoorOpenBegin));

            storyboard.Children.Add(CreateDoubleAnimation(
                HorizontalDrawerPanel,
                OpacityProperty,
                1,
                0.24,
                DoorOpenDuration,
                DrawerPullEase,
                DoorOpenBegin));
        }

        private void AddInteriorRevealAnimations(Storyboard storyboard, CabinetType cabinetType)
        {
            FrameworkElement interiorPanel = GetInteriorPanel(cabinetType);

            storyboard.Children.Add(CreateDoubleAnimation(
                interiorPanel,
                OpacityProperty,
                0.35,
                1,
                InteriorRevealDuration,
                InteriorRevealEase,
                InteriorRevealBegin));

            if (interiorPanel.RenderTransform is System.Windows.Media.ScaleTransform interiorScale)
            {
                storyboard.Children.Add(CreateDoubleAnimation(
                    interiorScale,
                    "ScaleX",
                    0.92,
                    1,
                    InteriorRevealDuration,
                    InteriorRevealEase,
                    InteriorRevealBegin));

                storyboard.Children.Add(CreateDoubleAnimation(
                    interiorScale,
                    "ScaleY",
                    0.92,
                    1,
                    InteriorRevealDuration,
                    InteriorRevealEase,
                    InteriorRevealBegin));
            }
        }

        private void AddPreviewCaptionAnimations(Storyboard storyboard)
        {
            storyboard.Children.Add(CreateDoubleAnimation(
                PreviewCaptionPanel,
                OpacityProperty,
                0,
                1,
                CaptionEnterDuration,
                InteriorRevealEase,
                CaptionEnterBegin));

            storyboard.Children.Add(CreateDoubleAnimation(
                PreviewCaptionTranslateTransform,
                "Y",
                8,
                0,
                CaptionEnterDuration,
                InteriorRevealEase,
                CaptionEnterBegin));
        }

        private void AddPreviewExitAnimations(Storyboard storyboard)
        {
            storyboard.Children.Add(CreateDoubleAnimation(
                CabinetPreviewLayer,
                OpacityProperty,
                1,
                0,
                PreviewExitDuration,
                PreviewExitEase,
                PreviewExitBegin));

            storyboard.Children.Add(CreateDoubleAnimation(
                PreviewScaleTransform,
                "ScaleX",
                1,
                1.03,
                PreviewExitDuration,
                PreviewExitEase,
                PreviewExitBegin));

            storyboard.Children.Add(CreateDoubleAnimation(
                PreviewScaleTransform,
                "ScaleY",
                1,
                1.03,
                PreviewExitDuration,
                PreviewExitEase,
                PreviewExitBegin));

            storyboard.Children.Add(CreateDoubleAnimation(
                PreviewBackdrop,
                OpacityProperty,
                0.72,
                0,
                PreviewExitDuration,
                PreviewExitEase,
                PreviewExitBegin));
        }

        private void AddSlotsRevealAnimations(Storyboard storyboard)
        {
            storyboard.Children.Add(CreateDoubleAnimation(
                SlotsHost,
                OpacityProperty,
                0,
                1,
                SlotsRevealDuration,
                SlotsRevealEase,
                SlotsRevealBegin));

            storyboard.Children.Add(CreateDoubleAnimation(
                SlotsScaleTransform,
                "ScaleX",
                0.96,
                1,
                SlotsRevealDuration,
                SlotsRevealEase,
                SlotsRevealBegin));

            storyboard.Children.Add(CreateDoubleAnimation(
                SlotsScaleTransform,
                "ScaleY",
                0.96,
                1,
                SlotsRevealDuration,
                SlotsRevealEase,
                SlotsRevealBegin));

            storyboard.Children.Add(CreateDoubleAnimation(
                SlotsTranslateTransform,
                "Y",
                12,
                0,
                SlotsRevealDuration,
                SlotsRevealEase,
                SlotsRevealBegin));
        }

        private void AddProgressAnimations(Storyboard storyboard)
        {
            storyboard.Children.Add(CreateDoubleAnimation(
                PreviewProgressScaleTransform,
                "ScaleX",
                0,
                1,
                ProgressDuration,
                ProgressEase));
        }

        private FrameworkElement GetInteriorPanel(CabinetType cabinetType)
        {
            return cabinetType switch
            {
                CabinetType.Vertical => VerticalInteriorPanel,
                CabinetType.Horizontal or CabinetType.MagneticDisk => HorizontalInteriorPanel,
                _ => StandardInteriorPanel
            };
        }

        private void CompleteCabinetOpenAnimation(CabinetType cabinetType)
        {
            CabinetPreviewLayer.Visibility = Visibility.Collapsed;
            CabinetPreviewLayer.Opacity = 0;
            PreviewScaleTransform.ScaleX = 1.03;
            PreviewScaleTransform.ScaleY = 1.03;
            PreviewShellScaleTransform.ScaleX = 1;
            PreviewShellScaleTransform.ScaleY = 1;
            PreviewBackdrop.Opacity = 0;
            PreviewProgressScaleTransform.ScaleX = 1;
            ApplyFinalPreviewState(cabinetType);

            SlotsHost.Opacity = 1;
            SlotsScaleTransform.ScaleX = 1;
            SlotsScaleTransform.ScaleY = 1;
            SlotsTranslateTransform.Y = 0;
        }

        private static void ResetInteriorScale(FrameworkElement interiorPanel)
        {
            if (interiorPanel.RenderTransform is System.Windows.Media.ScaleTransform scale)
            {
                scale.ScaleX = 0.92;
                scale.ScaleY = 0.92;
            }
        }

        private static DoubleAnimation CreateDoubleAnimation(
            DependencyObject target,
            DependencyProperty property,
            double from,
            double to,
            TimeSpan duration,
            IEasingFunction? easing = null,
            TimeSpan? beginTime = null)
        {
            return CreateDoubleAnimation(
                target,
                new PropertyPath(property),
                from,
                to,
                duration,
                easing,
                beginTime);
        }

        private static DoubleAnimation CreateDoubleAnimation(
            DependencyObject target,
            PropertyPath propertyPath,
            double from,
            double to,
            TimeSpan duration,
            IEasingFunction? easing = null,
            TimeSpan? beginTime = null)
        {
            var animation = new DoubleAnimation
            {
                From = from,
                To = to,
                Duration = duration,
                FillBehavior = FillBehavior.Stop,
                EasingFunction = easing
            };

            if (beginTime.HasValue)
            {
                animation.BeginTime = beginTime.Value;
            }

            Storyboard.SetTarget(animation, target);
            Storyboard.SetTargetProperty(animation, propertyPath);
            return animation;
        }

        private static DoubleAnimation CreateDoubleAnimation(
            DependencyObject target,
            string propertyName,
            double from,
            double to,
            TimeSpan duration,
            IEasingFunction? easing = null,
            TimeSpan? beginTime = null)
        {
            return CreateDoubleAnimation(
                target,
                new PropertyPath(propertyName),
                from,
                to,
                duration,
                easing,
                beginTime);
        }

        private void ApplyFinalPreviewState(CabinetType cabinetType)
        {
            switch (cabinetType)
            {
                case CabinetType.Vertical:
                    VerticalDoorTranslateTransform.X = 162;
                    VerticalDoorPanel.Opacity = 0.16;
                    VerticalInteriorPanel.Opacity = 1;
                    break;
                case CabinetType.Horizontal:
                case CabinetType.MagneticDisk:
                    HorizontalDrawerTranslateTransform.Y = 124;
                    HorizontalDrawerPanel.Opacity = 0.24;
                    HorizontalInteriorPanel.Opacity = 1;
                    break;
                default:
                    LeftDoorRotateTransform.Angle = -102;
                    RightDoorRotateTransform.Angle = 102;
                    LeftDoorPanel.Opacity = 0.2;
                    RightDoorPanel.Opacity = 0.2;
                    PreviewCenterDivider.Opacity = 0;
                    StandardInteriorPanel.Opacity = 1;
                    break;
            }
        }
    }
}
