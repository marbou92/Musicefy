// LibraryService.h
// Concrete implementation of ILibraryService that owns:
//   * the list of root folders the user has chosen to watch;
//   * the scan orchestrator (LibraryScanner);
//   * a small set of integration signals the UI layer subscribes to.
//
// Folder list is persisted in QSettings under "library/folders" as a
// QStringList. On construction the list is loaded (and non-existent
// folders pruned); on every mutation the list is saved back.
//
// Scanning runs on the caller's thread, but the per-file progress
// callback calls QCoreApplication::processEvents() so a long scan
// doesn't paint a frozen window. The scan is also cancellable.
//
// This service is the "library" half of the application — the read
// side (LibraryViewModel) keeps using LibraryRepository directly so
// the existing tests stay green, but anything that wants to mutate
// the library (add a folder, force a rescan) goes through here.

#pragma once

#include "../interfaces/ILibraryService.h"

#include <QObject>
#include <QString>
#include <QStringList>

#include <memory>

namespace mf::core::database {
    class LibraryRepository;
    class LibraryScanner;
    struct ScanProgress;
}

namespace mf::core::services {

class LibraryService : public QObject, public mf::core::interfaces::ILibraryService {
    Q_OBJECT
    Q_PROPERTY(QStringList folders READ folders NOTIFY foldersChanged)
    Q_PROPERTY(bool         scanning READ isScanning NOTIFY scanningChanged)
    Q_PROPERTY(int          totalAdded READ totalAdded NOTIFY scanFinished)
    Q_PROPERTY(int          totalUpdated READ totalUpdated NOTIFY scanFinished)
public:
    explicit LibraryService(mf::core::database::LibraryRepository* repo,
                            QObject* parent = nullptr);
    ~LibraryService() override;

    // ── ILibraryService ────────────────────────────────────────────────
    void initialize() override;
    void scanLibrary() override;
    void cancelScan() override;
    bool isScanning() const override { return scanning_; }

    QList<mf::core::models::MusicFile>    allTracks()       const override;
    QList<mf::core::models::ArtistInfo>   allArtists()      const override;
    QList<mf::core::models::AlbumInfo>    allAlbums()       const override;
    QList<mf::core::models::PlaylistInfo> allPlaylists()    const override;

    QList<mf::core::models::MusicFile> tracksForAlbum(QString albumId)  const override;
    QList<mf::core::models::AlbumInfo>  albumsForArtist(QString artistId) const override;
    QList<mf::core::models::MusicFile> tracksForArtist(QString artistId) const override;

    mf::core::models::MusicFile trackByPath(QString filePath) const override;
    void                        addTrack(mf::core::models::MusicFile track) override;
    void                        updateTrack(mf::core::models::MusicFile track) override;
    void                        removeTrack(QString filePath) override;
    void                        incrementPlayCount(QString filePath) override;
    void                        toggleFavourite(QString filePath) override;

    // ── Smart queries ──────────────────────────────────────────────────
    QList<mf::core::models::MusicFile> favouriteTracks(int limit = -1) const override;
    QList<mf::core::models::MusicFile> recentlyPlayedTracks(int limit = 10) const override;
    QList<mf::core::models::MusicFile> mostPlayedTracks(int limit = 10) const override;
    QList<mf::core::models::MusicFile> recentlyAddedTracks(int limit = 10) const override;
    QList<mf::core::models::MusicFile> forgottenFavourites(int limit = 10) const override;
    QList<mf::core::models::MusicFile> randomFavouriteTracks(int limit = 10) const override;

    void setOnScanProgress(ScanProgressCallback cb)  override { onScanProgress_ = cb; }
    void setOnTrackAdded(TrackAddedCallback cb)      override { onTrackAdded_    = cb; }
    void setOnTrackRemoved(TrackRemovedCallback cb)  override { onTrackRemoved_  = cb; }
    void setOnTrackUpdated(TrackUpdatedCallback cb)  override { onTrackUpdated_  = cb; }

    // ── Folder management (beyond the interface) ───────────────────────
    QStringList folders() const { return folders_; }

    /// Validate the path, normalize it, and append it to the folder
    /// list. Returns true on success; false (and emits no signals)
    /// if the path is empty, doesn't exist, isn't a directory, or is
    /// already in the list. Persists the new list immediately.
    Q_INVOKABLE bool addFolder(const QString& path);

    /// Remove the given path from the folder list, delete every track
    /// whose path starts with it, and persist. No-op (returns false)
    /// if the path isn't in the list. Cancels an in-flight scan if
    /// the removed folder was the only one being scanned.
    Q_INVOKABLE bool removeFolder(const QString& path);

    /// Force a rescan of every folder in the list.
    Q_INVOKABLE void rescan();

    int totalAdded()   const { return lastAdded_; }
    int totalUpdated() const { return lastUpdated_; }

signals:
    // Folder / scan state.
    void foldersChanged();
    void folderAdded(const QString& path);
    void folderRemoved(const QString& path);
    void scanningChanged();
    void tracksChanged();

    // Scan lifecycle.
    void scanStarted(const QStringList& folders);
    void scanProgress(int current, int total, const QString& currentFile);
    void scanFinished(int added, int updated);
    void scanCancelled();
    void scanFailed(const QString& error);

private slots:
    void onScanWorkerFinished();

private:
    void startScan();
    void loadFolders();
    void saveFolders() const;
    void deleteTracksUnder(const QString& folderPath);
    void onScannerProgress(const mf::core::database::ScanProgress& sp);

    mf::core::database::LibraryRepository* repo_ = nullptr;
    std::unique_ptr<mf::core::database::LibraryScanner> scanner_;

    QStringList folders_;
    bool        scanning_    = false;
    int         lastAdded_   = 0;
    int         lastUpdated_ = 0;
    bool        scanWasCancelled_ = false;

    // Interface callbacks.
    ScanProgressCallback   onScanProgress_;
    TrackAddedCallback     onTrackAdded_;
    TrackRemovedCallback   onTrackRemoved_;
    TrackUpdatedCallback   onTrackUpdated_;
};

} // namespace mf::core::services
