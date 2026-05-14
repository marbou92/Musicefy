using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace Musicefy.Controls
{
    public partial class ToggleSettingControl : UserControl
    {
        public ToggleSettingControl()
        {
            InitializeComponent();
        }

        public string Label
        {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }
        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register("Label", typeof(string), typeof(ToggleSettingControl));

        public string Description
        {
            get => (string)GetValue(DescriptionProperty);
            set => SetValue(DescriptionProperty, value);
        }
        public static readonly DependencyProperty DescriptionProperty =
            DependencyProperty.Register("Description", typeof(string), typeof(ToggleSettingControl));

        public bool IsChecked
        {
            get => (bool)GetValue(IsCheckedProperty);
            set => SetValue(IsCheckedProperty, value);
        }
        public static readonly DependencyProperty IsCheckedProperty =
            DependencyProperty.Register("IsChecked", typeof(bool), typeof(ToggleSettingControl),
                new PropertyMetadata(false, OnIsCheckedChanged));

        private static void OnIsCheckedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (ToggleSettingControl)d;
            bool isChecked = (bool)e.NewValue;

            var knob = control.SwitchKnob;
            var border = control.SwitchBorder;

            var knobAnim = new ThicknessAnimation
            {
                To = isChecked ? new Thickness(22, 2, 2, 2) : new Thickness(2, 2, 22, 2),
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };

            var colorAnim = new ColorAnimation
            {
                To = isChecked
                    ? (System.Windows.Media.Color)Application.Current.FindResource("AccentBrushColor")
                    : (System.Windows.Media.Color)Application.Current.FindResource("AccentPressedBrushColor"),
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };

            knob.BeginAnimation(MarginProperty, knobAnim);
            border.Background.BeginAnimation(System.Windows.Media.SolidColorBrush.ColorProperty, colorAnim);
        }
    }
}
