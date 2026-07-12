using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Musicefy.Core.Services;
using Musicefy.Services;

namespace Musicefy.ViewModels
{
    /// <summary>
    /// Sprint 6: ViewModel for Backup & Restore.
    /// Creates and restores .mbackup ZIP archives containing the SQLite DB,
    /// sources.json, queue_state.json, and settings.
    /// </summary>
    public class BackupRestoreViewModel : ViewModelBase
    {
        private readonly BackupService _backupService;
        private bool _isBusy;
        private string _statusText = "Ready";
        private string _lastBackupPath = "";

        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public string LastBackupPath
        {
            get => _lastBackupPath;
            set => SetProperty(ref _lastBackupPath, value);
        }

        public ICommand CreateBackupCommand { get; }
        public ICommand RestoreBackupCommand { get; }

        public BackupRestoreViewModel(BackupService backupService)
        {
            _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));

            CreateBackupCommand = new RelayCommand(async _ => await CreateBackupAsync());
            RestoreBackupCommand = new RelayCommand(async _ => await RestoreBackupAsync());

            LastBackupPath = Musicefy.Properties.Settings.Default.LastBackupPath ?? "";
        }

        public BackupRestoreViewModel() : this(
            App.Services.GetService<BackupService>())
        {
        }

        public async Task CreateBackupAsync()
        {
            using var dialog = new System.Windows.Forms.SaveFileDialog
            {
                Filter = "Musicefy Backup|*.mbackup",
                DefaultExt = ".mbackup",
                FileName = $"musicefy-backup-{DateTime.Now:yyyy-MM-dd}.mbackup"
            };

            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            IsBusy = true;
            StatusText = "Creating backup…";

            try
            {
                var path = await _backupService.CreateBackupAsync(dialog.FileName);
                LastBackupPath = path;
                Musicefy.Properties.Settings.Default.LastBackupPath = path;
                Musicefy.Properties.Settings.Default.Save();

                var size = _backupService.GetBackupSize(path);
                StatusText = $"Backup created: {BackupService.FormatFileSize(size)}";
                ToastService.ShowToast($"Backup created successfully.", System.Windows.Media.Brushes.ForestGreen);
            }
            catch (Exception ex)
            {
                StatusText = $"Backup failed: {ex.Message}";
                ToastService.ShowToast($"Backup failed: {ex.Message}", System.Windows.Media.Brushes.OrangeRed);
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task RestoreBackupAsync()
        {
            using var dialog = new System.Windows.Forms.OpenFileDialog
            {
                Filter = "Musicefy Backup|*.mbackup",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            var confirm = System.Windows.MessageBox.Show(
                "Restoring will overwrite your current database, sources, and queue.\n\n" +
                "The app will need to restart after restore.\n\nContinue?",
                "Confirm Restore",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (confirm != System.Windows.MessageBoxResult.Yes) return;

            IsBusy = true;
            StatusText = "Restoring backup…";

            try
            {
                await _backupService.RestoreBackupAsync(dialog.FileName);
                StatusText = "Backup restored. Please restart Musicefy.";
                ToastService.ShowToast("Backup restored. Restart the app to apply.", System.Windows.Media.Brushes.ForestGreen);

                System.Windows.MessageBox.Show(
                    "Backup restored successfully.\n\nPlease restart Musicefy to apply the changes.",
                    "Restore Complete",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusText = $"Restore failed: {ex.Message}";
                ToastService.ShowToast($"Restore failed: {ex.Message}", System.Windows.Media.Brushes.OrangeRed);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
