using System.Windows;

namespace Musicefy.Views
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
        }

        private void ThemeCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var selected = (ThemeCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content.ToString();
            if (selected == "Light")
            {
                ApplyTheme("Themes/Light.xaml");
            }
            else if (selected == "Dark")
            {
                ApplyTheme("Themes/Dark.xaml");
            }
        }

        private void ApplyTheme(string themePath)
        {
            var dict = new ResourceDictionary { Source = new System.Uri(themePath, System.UriKind.Relative) };
            Application.Current.Resources.MergedDictionaries.Clear();
            Application.Current.Resources.MergedDictionaries.Add(dict);
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Persist settings (Properties.Settings or JSON)
            this.DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            Close();
        }
    }
}
