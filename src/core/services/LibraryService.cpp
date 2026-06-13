// LibraryService.cpp
// See header. See ILibraryService.h for the interface contract.

#include "LibraryService.h"

#include "../database/LibraryRepository.h"
#include "../database/LibraryScanner.h"

#include <QCoreApplication>
#include <QDir>
#include <QFileInfo>
#include <QSettings>
#include <algorithm>
#include <QtGlobal>

namespace mf::core::services {

using mf::core::database::LibraryRepository;
using mf::core::database::LibraryScanner;
using mf::core::database::ScanProgress;
using mf::core::models::MusicFile;
using mf::core::models::ArtistInfo;
using mf::core::models::AlbumInfo;
using mf::core::models::PlaylistInfo;

namespace {
constexpr const char* kFoldersKey = "library/folders";
}

LibraryService::LibraryService(LibraryRepository* repo, QObject* parent)
    : QObject(parent)
    , repo_(repo)
    , scanner_(std::make_unique<LibraryScanner>(repo_->database()))
{
    loadFolders();
}

LibraryService::~LibraryService() {
    if (scanning_) {
        scanner_->cancel();
    }
}

void LibraryService::initialize() {
    // Nothing to do beyond the constructor today; reserved for future
    // startup tasks (e.g. lazy-load artwork cache, prime the FTS index).
}

void LibraryService::loadFolders() {
    QSettings s;
    folders_ = s.value(QLatin1String(kFoldersKey)).toStringList();
    // Prune any folders that no longer exist on disk.
    QStringList pruned;
    pruned.reserve(folders_.size());
    for (const QString& p : folders_) {
        if (QDir(p).exists()) {
            pruned.append(p);
        }
    }
    if (pruned != folders_) {
        folders_ = pruned;
        saveFolders();
    }
    emit foldersChanged();
}

void LibraryService::saveFolders() const {
    QSettings s;
    s.setValue(QLatin1String(kFoldersKey), folders_);
    s.sync();
}

bool LibraryService::addFolder(const QString& path) {
    if (path.isEmpty()) return false;
    QString clean = QDir::cleanPath(path);
    QFileInfo fi(clean);
    if (!fi.exists() || !fi.isDir()) return false;
    if (folders_.contains(clean)) return false;
    folders_.append(clean);
    saveFolders();
    emit foldersChanged();
    emit folderAdded(clean);
    // Start a scan of all folders so the user sees immediate progress.
    startScan();
    return true;
}

bool LibraryService::removeFolder(const QString& path) {
    QString clean = QDir::cleanPath(path);
    int idx = folders_.indexOf(clean);
    if (idx < 0) return false;
    folders_.removeAt(idx);
    saveFolders();
    emit foldersChanged();
    emit folderRemoved(clean);
    if (scanning_ && folders_.isEmpty()) {
        scanner_->cancel();
    }
    deleteTracksUnder(clean);
    emit tracksChanged();
    return true;
}

void LibraryService::deleteTracksUnder(const QString& folderPath) {
    if (repo_) {
        repo_->deleteTracksByPathPrefix(folderPath);
    }
}

void LibraryService::rescan() {
    startScan();
}

void LibraryService::cancelScan() {
    if (!scanning_) return;
    scanner_->cancel();
    scanWasCancelled_ = true;
    // The scanner will return from scan() shortly, at which point
    // onScanWorkerFinished will set scanning_ = false and emit
    // scanCancelled.
}

void LibraryService::scanLibrary() {
    startScan();
}

void LibraryService::startScan() {
    if (scanning_) return;
    if (folders_.isEmpty()) return;

    scanning_         = false; // becomes true when the worker starts
    scanWasCancelled_ = false;
    lastAdded_        = 0;
    lastUpdated_      = 0;

    emit scanStarted(folders_);

    // The scanner's progress callback is called once per file, on the
    // thread that runs scan(). We forward each event to a Qt signal so
    // UI subscribers and tests can use QSignalSpy, and pump the event
    // loop so the UI doesn't freeze.
    auto onProgress = [this](const ScanProgress& sp) {
        emit scanProgress(sp.current, sp.total, sp.currentFile);
        if (onScanProgress_) onScanProgress_(sp.current, sp.total, sp.currentFile);
        if (QCoreApplication::instance()) {
            QCoreApplication::processEvents(QEventLoop::AllEvents, 50);
        }
    };

    scanning_ = true;
    emit scanningChanged();
    scanner_->scan(folders_, onProgress);
    // scan() is synchronous: by this point the worker is done.
    onScanWorkerFinished();
}

void LibraryService::onScanWorkerFinished() {
    const bool wasCancelled = scanner_->isCancelled() || scanWasCancelled_;
    lastAdded_   = scanner_->totalAdded();
    lastUpdated_ = scanner_->totalUpdated();

    scanning_ = false;
    emit scanningChanged();

    if (wasCancelled) {
        emit scanCancelled();
    } else {
        emit scanFinished(lastAdded_, lastUpdated_);
        emit tracksChanged();
    }
}

void LibraryService::onScannerProgress(const ScanProgress& sp) {
    // Direct entry point (used by tests that want to fire progress
    // callbacks without doing a real scan). The in-line lambda in
    // startScan() already forwards to scanProgress, so this is a
    // public re-entry point for synthetic progress events.
    emit scanProgress(sp.current, sp.total, sp.currentFile);
    if (onScanProgress_) {
        onScanProgress_(sp.current, sp.total, sp.currentFile);
    }
}

// ── ILibraryService read-side pass-throughs ───────────────────────────

QList<MusicFile>    LibraryService::allTracks()     const { return repo_ ? repo_->allTracks()     : QList<MusicFile>{}; }
QList<ArtistInfo>   LibraryService::allArtists()    const { return repo_ ? repo_->allArtists()    : QList<ArtistInfo>{}; }
QList<AlbumInfo>    LibraryService::allAlbums()     const { return repo_ ? repo_->allAlbums()     : QList<AlbumInfo>{}; }
QList<PlaylistInfo> LibraryService::allPlaylists()  const { return repo_ ? repo_->allPlaylists()  : QList<PlaylistInfo>{}; }

QList<MusicFile> LibraryService::tracksForAlbum(QString albumId) const {
    return repo_ ? repo_->tracksForAlbum(albumId) : QList<MusicFile>{};
}
QList<AlbumInfo> LibraryService::albumsForArtist(QString artistId) const {
    return repo_ ? repo_->albumsForArtist(artistId) : QList<AlbumInfo>{};
}
QList<MusicFile> LibraryService::tracksForArtist(QString artistId) const {
    return repo_ ? repo_->tracksForArtist(artistId) : QList<MusicFile>{};
}

MusicFile LibraryService::trackByPath(QString filePath) const {
    if (!repo_) return MusicFile{};
    auto m = repo_->trackByPath(filePath);
    return m.value_or(MusicFile{});
}

void LibraryService::addTrack(MusicFile track) {
    if (!repo_) return;
    repo_->upsertTrack(track);
    if (onTrackAdded_) onTrackAdded_(track);
    emit tracksChanged();
}

void LibraryService::updateTrack(MusicFile track) {
    if (!repo_) return;
    repo_->upsertTrack(track);
    if (onTrackUpdated_) onTrackUpdated_(track);
    emit tracksChanged();
}

void LibraryService::removeTrack(QString filePath) {
    if (!repo_) return;
    repo_->deleteTrack(filePath);
    if (onTrackRemoved_) onTrackRemoved_(filePath);
    emit tracksChanged();
}

void LibraryService::incrementPlayCount(QString filePath) {
    if (!repo_) return;
    repo_->incrementPlayCount(filePath);
}

void LibraryService::toggleFavourite(QString filePath) {
    if (!repo_) return;
    repo_->toggleFavourite(filePath);
    emit tracksChanged();
}

// ── Smart queries ──────────────────────────────────────────────────

QList<MusicFile> LibraryService::favouriteTracks(int limit) const {
    if (!repo_) return {};
    auto tracks = repo_->favouriteTracks();
    if (limit > 0 && tracks.size() > limit) {
        tracks = tracks.mid(0, limit);
    }
    return tracks;
}

QList<MusicFile> LibraryService::recentlyPlayedTracks(int limit) const {
    if (!repo_) return {};
    return repo_->recentlyPlayedTracks(limit);
}

QList<MusicFile> LibraryService::mostPlayedTracks(int limit) const {
    if (!repo_) return {};
    auto tracks = repo_->allTracks();
    // Sort by play_count descending.
    std::sort(tracks.begin(), tracks.end(),
        [](const MusicFile& a, const MusicFile& b) {
            return a.playCount() > b.playCount();
        });
    if (limit > 0 && tracks.size() > limit) {
        tracks = tracks.mid(0, limit);
    }
    return tracks;
}

QList<MusicFile> LibraryService::recentlyAddedTracks(int limit) const {
    if (!repo_) return {};
    auto tracks = repo_->allTracks();
    // Sort by date_added descending.
    std::sort(tracks.begin(), tracks.end(),
        [](const MusicFile& a, const MusicFile& b) {
            return a.dateAdded().value_or(QDateTime()) > b.dateAdded().value_or(QDateTime());
        });
    if (limit > 0 && tracks.size() > limit) {
        tracks = tracks.mid(0, limit);
    }
    return tracks;
}

QList<MusicFile> LibraryService::forgottenFavourites(int limit) const {
    if (!repo_) return {};
    auto favs = repo_->favouriteTracks();
    // Filter to tracks with low play counts (forgotten = favourite but rarely played).
    QList<MusicFile> forgotten;
    for (const auto& t : favs) {
        if (t.playCount() <= 2) {
            forgotten.append(t);
        }
    }
    if (limit > 0 && forgotten.size() > limit) {
        forgotten = forgotten.mid(0, limit);
    }
    return forgotten;
}

QList<MusicFile> LibraryService::randomFavouriteTracks(int limit) const {
    if (!repo_) return {};
    auto favs = repo_->favouriteTracks();
    // Fisher-Yates shuffle, limited to requested count.
    for (int i = favs.size() - 1; i > 0; --i) {
        int j = qrand() % (i + 1);
        std::swap(favs[i], favs[j]);
    }
    if (limit > 0 && favs.size() > limit) {
        favs = favs.mid(0, limit);
    }
    return favs;
}

} // namespace mf::core::services