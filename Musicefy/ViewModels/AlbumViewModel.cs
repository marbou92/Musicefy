using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Musicefy.Core.Interfaces;
using Musicefy.Core.Models;
using Musicefy.Core.Services;

namespace Musicefy.ViewModels
{
    public class AlbumViewModel : ViewModelBase
    {
        private readonly IAudioPlayer _playback;
        private readonly ArtistAlbumService _artistAlbumService;

        private string _albumName;
        public string AlbumName
        {
            get => _albumName;
            set => SetProperty(ref _albumName, value);
        }

        private string _artistName;
        public string ArtistName
        {
            get => _artistName;
            set => SetProperty(ref _artistName, value);
        }

        private int _year;
        public int Year
        {
            get => _year;
            set => SetProperty(ref _year, value);
        }

        private string _coverPath;
        public string CoverPath
        {
            get => _coverPath;
            set => SetProperty(ref _coverPath, value);
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public ObservableCollection<MusicFile> Tracks { get; } = new ObservableCollection<MusicFile>();

        public ICommand PlayAllCommand { get; }
        public ICommand PlayTrackCommand { get; }

        public AlbumViewModel(IAudioPlayer playback, ArtistAlbumService artistAlbumService)
        {
            _playback = playback;
            _artistAlbumService = artistAlbumService;

            PlayAllCommand = new RelayCommand(_ => ExecutePlayAll());
            PlayTrackCommand = new RelayCommand(p => { if (p is MusicFile t) _playback.PlayTrack(t); });
        }

        public async Task LoadAsync(string albumName, string artistName = null)
        {
            AlbumName = albumName;
            ArtistName = artistName;
            IsLoading = true;
            Tracks.Clear();

            try
            {
                var album = await _artistAlbumService.GetAlbumDetailAsync(albumName, artistName);
                if (album != null)
                {
                    CoverPath = album.CoverPath;
                    Year = album.Year;
                    ArtistName = album.Artist;
                    foreach (var track in album.Tracks)
                        Tracks.Add(track);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AlbumViewModel] Load failed: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ExecutePlayAll()
        {
            if (Tracks.Count > 0)
                _playback.SetQueue(Tracks, startPlaying: true);
        }
    }
}
