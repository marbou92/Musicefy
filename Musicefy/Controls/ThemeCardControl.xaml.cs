using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Musicefy.Controls
{
    public partial class ThemeCardControl : UserControl
    {
        public ThemeCardControl() => InitializeComponent();

        // Renamed to avoid conflict with FrameworkElement.Name
        public string CardName
        {
            get => (string)GetValue(CardNameProperty);
            set => SetValue(CardNameProperty, value);
        }

        public static readonly DependencyProperty CardNameProperty =
            DependencyProperty.Register("CardName", typeof(string), typeof(ThemeCardControl));

        public Brush PreviewBrush
        {
            get => (Brush)GetValue(PreviewBrushProperty);
            set => SetValue(PreviewBrushProperty, value);
        }

        public static readonly DependencyProperty PreviewBrushProperty =
            DependencyProperty.Register("PreviewBrush", typeof(Brush), typeof(ThemeCardControl));

        public bool IsSelected
        {
            get => (bool)GetValue(IsSelectedProperty);
            set => SetValue(IsSelectedProperty, value);
        }

        public static readonly DependencyProperty IsSelectedProperty =
            DependencyProperty.Register("IsSelected", typeof(bool), typeof(ThemeCardControl),
                new PropertyMetadata(false, OnSelectedChanged));

        private static void OnSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var card = (ThemeCardControl)d;
            card.CheckMark.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
            card.CardBorder.BorderBrush = (bool)e.NewValue
                ? (Brush)Application.Current.FindResource("AccentBrush")
                : Brushes.Transparent;
        }
    }
}
