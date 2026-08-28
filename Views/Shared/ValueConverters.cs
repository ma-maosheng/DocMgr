using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DocMgr.Views.Shared
{
    /// <summary>
    /// 将页面 DataContext 桥接到不在可视树中的对象（如 <see cref="System.Windows.Controls.DataGridColumn"/>）以便绑定。
    /// </summary>
    public sealed class BindingProxy : Freezable
    {
        public static readonly DependencyProperty DataProperty = DependencyProperty.Register(
            nameof(Data),
            typeof(object),
            typeof(BindingProxy));

        public object? Data
        {
            get => GetValue(DataProperty);
            set => SetValue(DataProperty, value);
        }

        protected override Freezable CreateInstanceCore() => new BindingProxy();
    }

    public class BoolToVisConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool visible)
            {
                return visible ? Visibility.Visible : Visibility.Collapsed;
            }

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class InverseBoolToVisConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool visible)
            {
                return visible ? Visibility.Collapsed : Visibility.Visible;
            }

            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// true → N*（默认 1，可通过 ConverterParameter 指定），false → 0，用于折叠侧栏列宽绑定。
    /// </summary>
    public sealed class BoolToStarOrZeroGridLengthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not bool expanded || !expanded)
            {
                return new GridLength(0);
            }

            double stars = 1;
            if (parameter != null
                && double.TryParse(parameter.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed)
                && parsed > 0)
            {
                stars = parsed;
            }

            return new GridLength(stars, GridUnitType.Star);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class CountToBoolConverter : IValueConverter
    {
        public static CountToBoolConverter Instance { get; } = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is int count && count > 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// 与「是」Radio 配对：绑定取反；仅在选中「否」时回写 false，取消选中时不回写（由「是」绑定更新）。
    /// </summary>
    public sealed class InverseBoolRadioConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? !b : true;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isChecked && isChecked)
            {
                return false;
            }

            return Binding.DoNothing;
        }
    }
}
