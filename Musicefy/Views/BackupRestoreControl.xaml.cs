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
    }
}
