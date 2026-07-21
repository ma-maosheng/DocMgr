using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DocMgr.Views.Shared
{
    /// <summary>
    /// 整数微调输入：支持上下键与按钮增减，限制在 Min～Max。
    /// </summary>
    public partial class IntegerSpinnerControl : UserControl
    {
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
            nameof(Value),
            typeof(int),
            typeof(IntegerSpinnerControl),
            new FrameworkPropertyMetadata(
                0,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnValueChanged,
                CoerceValue));

        public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
            nameof(Minimum),
            typeof(int),
            typeof(IntegerSpinnerControl),
            new PropertyMetadata(0, OnRangeChanged));

        public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
            nameof(Maximum),
            typeof(int),
            typeof(IntegerSpinnerControl),
            new PropertyMetadata(int.MaxValue, OnRangeChanged));

        public IntegerSpinnerControl()
        {
            InitializeComponent();
            Loaded += (_, _) => SyncTextFromValue();
        }

        public int Value
        {
            get => (int)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public int Minimum
        {
            get => (int)GetValue(MinimumProperty);
            set => SetValue(MinimumProperty, value);
        }

        public int Maximum
        {
            get => (int)GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }

        private static object CoerceValue(DependencyObject d, object baseValue)
        {
            var control = (IntegerSpinnerControl)d;
            int value = (int)baseValue;
            int min = control.Minimum;
            int max = Math.Max(min, control.Maximum);
            return Math.Clamp(value, min, max);
        }

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (IntegerSpinnerControl)d;
            control.SyncTextFromValue();
            control.UpdateButtonStates();
        }

        private static void OnRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (IntegerSpinnerControl)d;
            control.CoerceValue(ValueProperty);
            control.UpdateButtonStates();
        }

        private void SyncTextFromValue()
        {
            if (ValueTextBox == null)
            {
                return;
            }

            string text = Value.ToString(CultureInfo.InvariantCulture);
            if (!string.Equals(ValueTextBox.Text, text, StringComparison.Ordinal))
            {
                ValueTextBox.Text = text;
            }
        }

        private void UpdateButtonStates()
        {
            if (IncreaseButton == null || DecreaseButton == null)
            {
                return;
            }

            bool enabled = IsEnabled;
            IncreaseButton.IsEnabled = enabled && Value < Maximum;
            DecreaseButton.IsEnabled = enabled && Value > Minimum;
        }

        private void IncreaseButton_Click(object sender, RoutedEventArgs e) => TryChangeValue(1);

        private void DecreaseButton_Click(object sender, RoutedEventArgs e) => TryChangeValue(-1);

        private void TryChangeValue(int delta)
        {
            if (!IsEnabled)
            {
                return;
            }

            Value = Math.Clamp(Value + delta, Minimum, Math.Max(Minimum, Maximum));
        }

        private void ValueTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!IsEnabled)
            {
                return;
            }

            if (e.Key == Key.Up)
            {
                TryChangeValue(1);
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                TryChangeValue(-1);
                e.Handled = true;
            }
        }

        private void ValueTextBox_LostFocus(object sender, RoutedEventArgs e) => CommitText();

        private void ValueTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = e.Text.Any(ch => !char.IsDigit(ch));
        }

        private void ValueTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsEnabled || ValueTextBox == null || !ValueTextBox.IsFocused)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(ValueTextBox.Text))
            {
                return;
            }

            if (int.TryParse(ValueTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                int clamped = Math.Clamp(parsed, Minimum, Math.Max(Minimum, Maximum));
                if (Value != clamped)
                {
                    Value = clamped;
                }
                else if (parsed != clamped)
                {
                    SyncTextFromValue();
                }
            }
        }

        private void CommitText()
        {
            if (ValueTextBox == null)
            {
                return;
            }

            if (!int.TryParse(ValueTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                SyncTextFromValue();
                return;
            }

            Value = Math.Clamp(parsed, Minimum, Math.Max(Minimum, Maximum));
            SyncTextFromValue();
        }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (e.Property == IsEnabledProperty)
            {
                UpdateButtonStates();
            }
        }
    }
}
