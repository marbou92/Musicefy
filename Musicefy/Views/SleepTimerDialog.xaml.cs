using System.Windows;

namespace Musicefy.Views
{
    public partial class SleepTimerDialog : Window
    {
        public int SelectedMinutes { get; private set; }

        public SleepTimerDialog()
        {
            InitializeComponent();
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (Rb15.IsChecked == true) SelectedMinutes = 15;
            else if (Rb30.IsChecked == true) SelectedMinutes = 30;
            else if (Rb60.IsChecked == true) SelectedMinutes = 60;
            else if (Rb90.IsChecked == true) SelectedMinutes = 90;
            else if (Rb120.IsChecked == true) SelectedMinutes = 120;
            else SelectedMinutes = 30;

            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
