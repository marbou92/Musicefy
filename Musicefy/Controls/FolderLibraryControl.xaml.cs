#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Musicefy.Core.Configuration;
using Musicefy.Core.Interfaces;
using Musicefy.Core.Library;
using Musicefy.Core.Models;
using Musicefy.Services;

namespace Musicefy.Controls;

public partial class FolderLibraryControl : UserControl
{
    // ── State ──────────────────────────────────────────────────────────────
    private PlaybackService?        _playbackService;
    private bool                    _isGridViewActive             = true;
    private readonly List<string>   _rootLibraryPaths             = [];
    private string?                 _currentBrowsingDirectoryPath;
    private readonly List<MusicFile> _currentLevelItemsCollection = [];

    private CancellationTokenSource? _scanCts;
    private bool                    _isScanToastVisible           = false;
    private bool                    _isLibraryLoaded;

    private readonly ILibraryService _libraryScanner;
    private readonly IFolderDataProvider _dataProvider;

    // ── Construction ───────────────────────────────────────────────────────
    public FolderLibraryControl()
    {
        InitializeComponent();
        _libraryScanner = App.Services.GetService<ILibraryService>()
            ?? new LibraryScanner(DatabaseConfig.ConnectionString);
        _dataProvider = App.Services.GetService<IFolderDataProvider>()
            ?? new SqliteFolderDataProvider(DatabaseConfig.ConnectionString);
        Loaded += FolderLibraryControl_Loaded;
    }

    private void FolderLibraryControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (_isLibraryLoaded) return;
        _isLibraryLoaded = true;

        var saved = LibraryControlSettings.Default.LastSelectedFolderPaths;
        if (!string.IsNullOrEmpty(saved))
        {
            foreach (var p in saved.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (Directory.Exists(p))
                    _rootLibraryPaths.Add(p);
            }
        }

        if (_rootLibraryPaths.Count > 0)
        {
            BtnClearFolder.Visibility = Visibility.Visible;
            RenderRootLibraryHubView();
        }
        else
        {
            BtnClearFolder.Visibility = Visibility.Collapsed;
            UpdateUiCollectionBindingStates(Enumerable.Empty<MusicFile>());
        }
    }

    // ── Public API (called from LibraryControl) ────────────────────────────
    public void InitializeDataStream(IEnumerable<MusicFile> tracks, PlaybackService playbackService)
    {
        _playbackService = playbackService;
        if (_rootLibraryPaths.Count > 0)
        {
            if (string.IsNullOrEmpty(_currentBrowsingDirectoryPath))
                RenderRootLibraryHubView();
            else
                NavigateToTargetDirectoryFolder(_currentBrowsingDirectoryPath);
        }
    }

    // ── Collection + view state ────────────────────────────────────────────
    private void UpdateUiCollectionBindingStates(IEnumerable<MusicFile> tracks)
    {
        var trackList = tracks?.ToList() ?? [];

        if (trackList.Count == 0)
        {
            EmptyLibraryStateContainer.Visibility = Visibility.Visible;
            ListViewContainer.Visibility          = Visibility.Collapsed;
            GridViewContainer.Visibility          = Visibility.Collapsed;
        }
        else
        {
            EmptyLibraryStateContainer.Visibility = Visibility.Collapsed;
            if (_isGridViewActive)
            {
                ListViewContainer.Visibility = Visibility.Collapsed;
                GridViewContainer.Visibility = Visibility.Visible;
            }
            else
            {
                GridViewContainer.Visibility = Visibility.Collapsed;
                ListViewContainer.Visibility = Visibility.Visible;
            }
        }

        FolderSongsListView.ItemsSource = trackList;
        FolderSongsGridBox.ItemsSource  = trackList;
    }

    // ── Navigation ─────────────────────────────────────────────────────────
    private async void RenderRootLibraryHubView()
    {
        _currentBrowsingDirectoryPath = null;

        if (BtnFolderBack.Visibility == Visibility.Visible)
        {
            var fadeOut = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromMilliseconds(150)));
            fadeOut.Completed += (s, e) => BtnFolderBack.Visibility = Visibility.Collapsed;
            BtnFolderBack.BeginAnimation(OpacityProperty, fadeOut);
        }

        await LoadAllTracksFromDatabaseAsync();
    }

    private async Task LoadAllTracksFromDatabaseAsync()
    {
        _currentLevelItemsCollection.Clear();

        // Pin root folder cards so users can drill into sub-folders
        foreach (var rootPath in _rootLibraryPaths)
        {
            if (Directory.Exists(rootPath))
            {
                _currentLevelItemsCollection.Add(new MusicFile
                {
                    Title      = Path.GetFileName(rootPath),
                    Artist     = "Root Library",
                    SourceType = "FolderItem",
                    FilePath   = rootPath,
                    SourceUri  = rootPath
                });
            }
        }

        try
        {
            var tracks = await _dataProvider.GetAllTracksAsync();
            _currentLevelItemsCollection.AddRange(tracks);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FolderLibraryControl] DB load failed: {ex.Message}");
        }

        UpdateUiCollectionBindingStates(_currentLevelItemsCollection);
    }

    /// <summary>
    /// Navigates into a subfolder with instant display. Uses a two-phase strategy:
    ///   Phase 1 — instant: single DB query fetches all tracks + builds per-folder
    ///              track counts, so subfolders and song lists appear immediately.
    ///   Phase 2 — async:  scans disk only for files NOT already in the DB,
    ///              then merges new tracks (no redundant TagLib reads).
    /// </summary>
    private async void NavigateToTargetDirectoryFolder(string? targetPath)
    {
        if (string.IsNullOrWhiteSpace(targetPath) || !Directory.Exists(targetPath)) return;

        _currentBrowsingDirectoryPath = targetPath;

        if (BtnFolderBack.Visibility != Visibility.Visible)
        {
            BtnFolderBack.Visibility = Visibility.Visible;
            BtnFolderBack.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(200))));
        }

        // ── Phase 1: Instant display from DB ──────────────────────────────
        var instantResults = new List<MusicFile>();
        var dirTrackCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        // Step 1: Discover subdirectories (fast, no metadata reads)
        string[] subDirs;
        try
        {
            subDirs = Directory.GetDirectories(targetPath);
            Array.Sort(subDirs, StringComparer.OrdinalIgnoreCase);
        }
        catch { subDirs = Array.Empty<string>(); }

        // Step 2: Single DB query for ALL tracks under this directory tree
        try
        {
            var allTracks = await _dataProvider.GetTracksByDirectoryAsync(targetPath);

            // Normalise once for reliable comparisons
            string targetFull  = Path.GetFullPath(targetPath);
            string targetSep   = targetFull + Path.DirectorySeparatorChar;

            foreach (var track in allTracks)
            {
                if (track.FilePath == null) continue;
                string? fileDir = Path.GetDirectoryName(track.FilePath);
                if (fileDir == null) continue;
                string fileDirFull = Path.GetFullPath(fileDir);

                if (string.Equals(fileDirFull, targetFull, StringComparison.OrdinalIgnoreCase))
                {
                    // Track sits directly in the target directory
                    instantResults.Add(track);
                }
                else if (fileDirFull.StartsWith(targetSep, StringComparison.OrdinalIgnoreCase))
                {
                    // Track is in a subdirectory — bucket count by immediate subfolder
                    string relative = fileDirFull.Substring(targetSep.Length);
                    int sep = relative.IndexOfAny(
                        new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
                    if (sep >= 0) relative = relative.Substring(0, sep);

                    string subDirFull = Path.GetFullPath(
                        targetFull + Path.DirectorySeparatorChar + relative);

                    if (!dirTrackCounts.ContainsKey(subDirFull))
                        dirTrackCounts[subDirFull] = 0;
                    dirTrackCounts[subDirFull]++;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[FolderLibraryControl] Instant DB load failed: {ex.Message}");
        }

        // Step 3: Build subfolder cards (DB count, fast fallback for unscanned dirs)
        foreach (var dir in subDirs)
        {
            var info = new DirectoryInfo(dir);
            if ((info.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden) continue;

            string dirFull = Path.GetFullPath(dir);
            int count = 0;

            if (!dirTrackCounts.TryGetValue(dirFull, out count))
            {
                // Fallback: count audio files up to 3 levels deep (no DB yet)
                count = CountFilesRecursive(dir, 0, 3);
            }

            instantResults.Add(new MusicFile
            {
                Title      = info.Name,
                Artist     = $"{count} track{(count != 1 ? "s" : "")}",
                SourceType = "FolderItem",
                FilePath   = dir,
                SourceUri  = dir
            });
        }

        // Show everything instantly
        _currentLevelItemsCollection.Clear();
        _currentLevelItemsCollection.AddRange(instantResults);
        UpdateUiCollectionBindingStates(_currentLevelItemsCollection);

        // ── Phase 2: Background scan for NEW files only ────────────────────
        var existingPaths = new HashSet<string>(
            instantResults.Where(x => x.SourceType == "FileItem")
                          .Select(x => x.FilePath),
            StringComparer.OrdinalIgnoreCase);

        _ = Task.Run(async () =>
        {
            try
            {
                // ScanDirectory skips files already in existingPaths
                // so no redundant TagLib reads happen
                var newItems = await Task.Run(() =>
                    _libraryScanner.ScanDirectory(targetPath, existingPaths));

                // Only keep the file items (subfolders were already handled)
                var newFiles = newItems.Where(x =>
                    x.SourceType == "FileItem" &&
                    !existingPaths.Contains(x.FilePath ?? ""))
                    .ToList();

                if (newFiles.Count > 0)
                {
                    Dispatcher.Invoke(() =>
                    {
                        foreach (var item in newFiles)
                            _currentLevelItemsCollection.Add(item);
                        UpdateUiCollectionBindingStates(_currentLevelItemsCollection);
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[FolderLibraryControl] Background folder scan failed: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Quick file count for folder items. Only counts audio files;
    /// caps recursion at maxDepth to avoid stalling on huge trees.
    /// </summary>
    private static int CountFilesRecursive(string path, int depth = 0, int maxDepth = 3)
    {
        if (depth > maxDepth) return 0;
        int count = 0;
        try
        {
            count += Directory.GetFiles(path)
                .Count(f => LibraryScannerExtensions.IsAudioFile(f));

            if (depth < maxDepth)
            {
                foreach (var dir in Directory.GetDirectories(path))
                    count += CountFilesRecursive(dir, depth + 1, maxDepth);
            }
        }
        catch
        {
            // Best-effort file count; skip inaccessible directories
        }
        return count;
    }

    // ── Background deep scan + toast ───────────────────────────────────────
    private void StartBackgroundScan(string scanPath)
    {
        _scanCts?.Cancel();
        _scanCts?.Dispose();
        _scanCts = new CancellationTokenSource();
        var token = _scanCts.Token;

        var progress = new Progress<ScanProgressInfo>(info =>
        {
            Dispatcher.Invoke(() =>
            {
                if (info.IsComplete)
                {
                    TxtScanHeadline.Text     = $"Done — {info.NewTracksAdded} new, {info.TracksUpdated} updated";
                    TxtScanCurrentFile.Text  = info.TracksRemoved > 0
                        ? $"{info.TracksRemoved} removed tracks pruned"
                        : "Library is up to date";
                    ScanProgressBar.Value    = 100;
                    TxtScanCounter.Text      = $"{info.TotalFiles} / {info.TotalFiles} files";

                    var timer = new System.Windows.Threading.DispatcherTimer
                    {
                        Interval = TimeSpan.FromSeconds(3)
                    };
                    timer.Tick += (s, e) =>
                    {
                        timer.Stop();
                        HideScanToast();
                    };
                    timer.Start();
                }
                else
                {
                    TxtScanHeadline.Text    = "Scanning library…";
                    TxtScanCurrentFile.Text = info.CurrentFileName;
                    ScanProgressBar.Value   = info.Percent;
                    TxtScanCounter.Text     = $"{info.FilesProcessed} / {info.TotalFiles} files";
                }
            });
        });

        ShowScanToast();

        Task.Run(async () =>
        {
            try
            {
                await _libraryScanner.ScanLibraryDeepAsync(scanPath, progress, token);

                if (!token.IsCancellationRequested)
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (string.IsNullOrEmpty(_currentBrowsingDirectoryPath))
                            RenderRootLibraryHubView();
                    });
                }
            }
            catch (OperationCanceledException)
            {
                Dispatcher.Invoke(HideScanToast);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FolderLibraryControl] Scan error: {ex.Message}");
                Dispatcher.Invoke(HideScanToast);
            }
        }, token);
    }

    // ── Toast animation helpers ────────────────────────────────────────────
    private void ShowScanToast()
    {
        if (_isScanToastVisible) return;
        _isScanToastVisible = true;

        ScanToastCard.Visibility = Visibility.Visible;
        ScanToastCard.Opacity    = 0;
        ScanToastTranslate.X     = 400;

        ScanToastCard.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(300)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
        ScanToastTranslate.BeginAnimation(TranslateTransform.XProperty,
            new DoubleAnimation(400, 0, new Duration(TimeSpan.FromMilliseconds(380)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
    }

    private void HideScanToast()
    {
        if (!_isScanToastVisible) return;

        var slideOut = new DoubleAnimation(0, 400, new Duration(TimeSpan.FromMilliseconds(300)))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        var fadeOut = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromMilliseconds(250)))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };

        fadeOut.Completed += (s, e) =>
        {
            ScanToastCard.Visibility = Visibility.Collapsed;
            _isScanToastVisible      = false;

            TxtScanHeadline.Text    = "Scanning library…";
            TxtScanCurrentFile.Text = "";
            ScanProgressBar.Value   = 0;
            TxtScanCounter.Text     = "0 / 0 files";
        };

        ScanToastTranslate.BeginAnimation(TranslateTransform.XProperty, slideOut);
        ScanToastCard.BeginAnimation(OpacityProperty, fadeOut);
    }

    // ── Button handlers ────────────────────────────────────────────────────
    private void BtnFolderBack_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentBrowsingDirectoryPath)) return;

        var parentDir = Directory.GetParent(_currentBrowsingDirectoryPath);
        if (parentDir != null && _rootLibraryPaths.Any(
                r => parentDir.FullName.StartsWith(r, StringComparison.OrdinalIgnoreCase)))
        {
            NavigateToTargetDirectoryFolder(parentDir.FullName);
        }
        else
        {
            RenderRootLibraryHubView();
        }
    }

    private void BtnAddFolder_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select a local music folder to add to your Musicefy library"
        };

        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

        string selectedPath = dialog.SelectedPath;

        if (!_rootLibraryPaths.Contains(selectedPath, StringComparer.OrdinalIgnoreCase))
            _rootLibraryPaths.Add(selectedPath);

        SaveRootPaths();

        BtnClearFolder.Visibility = Visibility.Visible;

        RenderRootLibraryHubView();
        StartBackgroundScan(selectedPath);
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_currentBrowsingDirectoryPath))
            NavigateToTargetDirectoryFolder(_currentBrowsingDirectoryPath);
        else if (_rootLibraryPaths.Count > 0)
        {
            RenderRootLibraryHubView();
            foreach (var path in _rootLibraryPaths)
                StartBackgroundScan(path);
        }
    }

    private void BtnClearFolder_Click(object sender, RoutedEventArgs e)
    {
        _scanCts?.Cancel();
        HideScanToast();

        _rootLibraryPaths.Clear();
        _currentBrowsingDirectoryPath = null;

        SaveRootPaths();

        BtnClearFolder.Visibility = Visibility.Collapsed;
        BtnFolderBack.Visibility  = Visibility.Collapsed;

        _currentLevelItemsCollection.Clear();
        UpdateUiCollectionBindingStates(_currentLevelItemsCollection);

        Task.Run(async () =>
        {
            try
            {
                using var conn = new Microsoft.Data.Sqlite.SqliteConnection(
                    DatabaseConfig.ConnectionString);
                await conn.ExecuteAsync("DELETE FROM Tracks");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FolderLibraryControl] Clear tracks failed: {ex.Message}");
            }
        });
    }

    private void SaveRootPaths()
    {
        LibraryControlSettings.Default.LastSelectedFolderPaths =
            string.Join(";", _rootLibraryPaths);
        LibraryControlSettings.Default.Save();
    }

    private void BtnViewToggle_Click(object sender, RoutedEventArgs e)
    {
        _isGridViewActive = !_isGridViewActive;

        ToggleIconPath.Data = _isGridViewActive
            ? Geometry.Parse("M3,5H21V7H3V5M3,11H21V13H3V11M3,17H21V19H3V17Z")
            : Geometry.Parse("M3,3H11V11H3V3M13,3H21V11H13V3M3,13H11V21H3V13M13,13H21V21H13V13Z");

        UpdateUiCollectionBindingStates(_currentLevelItemsCollection);
    }

    // ── Item interaction ───────────────────────────────────────────────────
    private void OnSongDoubleClicked(object sender, MouseButtonEventArgs e)
    {
        var selected = FolderSongsListView.SelectedItem as MusicFile
                    ?? FolderSongsGridBox.SelectedItem as MusicFile;

        if (selected != null)
            HandleItemSelection(selected);
    }

    private void GridCardItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is MusicFile selectedItem)
            HandleItemSelection(selectedItem);
    }

    private void HandleItemSelection(MusicFile item)
    {
        if (item.SourceType == "FolderItem" && item.FilePath is not null)
            NavigateToTargetDirectoryFolder(item.FilePath);
        else if (item.SourceType == "FileItem")
            _playbackService?.PlayTrackWithDirectory(item);
    }
}

// ── Per-control persistent settings ───────────────────────────────────────
public class LibraryControlSettings : System.Configuration.ApplicationSettingsBase
{
    private static readonly LibraryControlSettings DefaultInstance =
        (LibraryControlSettings)Synchronized(new LibraryControlSettings());
    public static LibraryControlSettings Default => DefaultInstance;

    [System.Configuration.UserScopedSetting]
    [System.Diagnostics.DebuggerNonUserCode]
    [System.Configuration.DefaultSettingValue("")]
    public string LastSelectedFolderPaths
    {
        get => (string)this["LastSelectedFolderPaths"];
        set => this["LastSelectedFolderPaths"] = value;
    }
}

/// <summary>
/// Extension helper to expose audio format checking for the folder file counter.
/// </summary>
internal static class LibraryScannerExtensions
{
    private static readonly HashSet<string> AudioExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".flac", ".wav", ".m4a", ".aac", ".ogg", ".opus",
        ".wma", ".ape", ".mpc", ".wv", ".aiff", ".aif", ".dsf"
    };

    public static bool IsAudioFile(string path)
        => AudioExtensions.Contains(Path.GetExtension(path));
}
