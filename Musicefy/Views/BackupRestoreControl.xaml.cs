using System.Windows.Controls;
using Musicefy.ViewModels;

namespace Musicefy.Views
{
    public partial class BackupRestoreControl : UserControl
    {
        public BackupRestoreControl()
        {
            InitializeComponent();
            if (DataContext == null)
            {
                try { DataContext = App.Services?.GetService<BackupRestoreViewModel>(); }
                catch { }
            }
        }

        private void CreateBackup_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            (DataContext as BackupRestoreViewModel)?.CreateBackupCommand.Execute(null);
        }

        private void RestoreBackup_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            (DataContext as BackupRestoreViewModel)?.RestoreBackupCommand.Execute(null);
        }
    }
}
