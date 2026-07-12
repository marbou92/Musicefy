using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Musicefy.Core.Services;

namespace Musicefy.ViewModels
{
    /// <summary>
    /// Sprint 5: ViewModel for the Listen History screen.
    /// Displays recent play events from the PlayEvents table.
    /// </summary>
    public class HistoryViewModel : ViewModelBase
    {
        private readonly HistoryService _historyService;
        private bool _isLoading;
        private ObservableCollection<HistoryEntry> _entries = new ObservableCollection<HistoryEntry>();

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public ObservableCollection<HistoryEntry> Entries
        {
            get => _entries;
            set => SetProperty(ref _entries, value);
        }

        public ICommand RefreshCommand { get; }
        public ICommand ClearHistoryCommand { get; }

        public HistoryViewModel(HistoryService historyService)
        {
            _historyService = historyService ?? throw new ArgumentNullException(nameof(historyService));

            RefreshCommand = new RelayCommand(async _ => await LoadAsync());
            ClearHistoryCommand = new RelayCommand(async _ => await ClearAsync());

            _ = LoadAsync();
        }

        public HistoryViewModel() : this(
            App.Services.GetService<HistoryService>())
        {
        }

        public async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                var entries = await _historyService.GetRecentHistoryAsync(200);
                Entries = new ObservableCollection<HistoryEntry>(entries);
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task ClearAsync()
        {
            var result = System.Windows.MessageBox.Show(
                "Are you sure you want to clear all listen history? This action cannot be undone.",
                "Clear History",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (result != System.Windows.MessageBoxResult.Yes) return;

            await _historyService.ClearHistoryAsync();
            await LoadAsync();
        }
    }
}
