// PlaylistView.h
// Full playlist overlay. Reachable via NavigationService::
//   playlistNavigationRequested(PlaylistInfo).
//
// The widget is a thin view over PlaylistViewModel. It binds to
// infoChanged / tracksChanged signals for re-renders, and routes
// play / shuffle clicks through the view model's Q_INVOKABLE
// methods. The view model is responsible for the queue work; the
// widget owns the styling and the track-list model.

#pragma once

#include "../../core/models/PlaylistInfo.h"

#include <QWidget>

class QLabel;
class QListView;
class QPushButton;
class QStandardItemModel;
class QStringList;

namespace mf::core::database    { class LibraryRepository; }
namespace mf::core::playback     { class QueueManager; }
namespace mf::core::services     { class NavigationService; class ImageCache; }
namespace mf::core::theme        { class ThemeManager; }
namespace mf::app::viewmodels    { class PlaylistViewModel; class LibraryViewModel; }
namespace mf::app::widgets       { class CoverImage; }

namespace mf::app::widgets {

class PlaylistView : public QWidget {
    Q_OBJECT
public:
    PlaylistView(mf::app::viewmodels::PlaylistViewModel* vm,
                 mf::core::services::NavigationService* nav,
                 mf::core::playback::QueueManager*     queue,
                 mf::core::services::ImageCache*       imageCache,
                 mf::core::database::LibraryRepository* repo,
                 mf::core::theme::ThemeManager*        theme,
                 mf::app::viewmodels::LibraryViewModel* libVm = nullptr,
                 QWidget* parent = nullptr);
    ~PlaylistView() override = default;

public slots:
    void setPlaylist(const mf::core::models::PlaylistInfo& info);

private slots:
    void onBackClicked();
    void onPlayAllClicked();
    void onShuffleClicked();
    void onRenameClicked();
    void onAddTracksClicked();
    void onFromPlaylistClicked();
    void onTrackListClicked(const QModelIndex& idx);
    void onTracksModelRowsMoved(const QModelIndex& parent, int start, int end,
                                const QModelIndex& destination, int row);
    void onTrackDoubleClicked(const QModelIndex& idx);
    void onShuffleChanged(bool on);
    void onVmInfoChanged();
    void onVmTracksChanged();
    void onVmErrorReported(const QString& message);

private:
    void buildUi();
    void applyTheme();
    void renderPlaylist();
    void refreshTrackModel();
    void addFilePaths(const QStringList& paths);
    QString formatTotalDuration(std::chrono::seconds s) const;

    mf::app::viewmodels::PlaylistViewModel* vm_           = nullptr;
    mf::core::services::NavigationService* nav_           = nullptr;
    mf::core::playback::QueueManager*     queue_          = nullptr;
    mf::core::services::ImageCache*       imageCache_     = nullptr;
    mf::core::database::LibraryRepository* repo_          = nullptr;
    mf::core::theme::ThemeManager*        theme_          = nullptr;
    mf::app::viewmodels::LibraryViewModel* libVm_         = nullptr;

    QPushButton*        backBtn_       = nullptr;
    QPushButton*        playAllBtn_    = nullptr;
    QPushButton*        shuffleBtn_    = nullptr;
    QPushButton*        renameBtn_     = nullptr;
    QPushButton*        addTracksBtn_  = nullptr;
    QPushButton*        fromPlaylistBtn_ = nullptr;
    CoverImage*         coverLabel_    = nullptr;
    QLabel*             titleLabel_    = nullptr;
    QLabel*             subtitleLabel_ = nullptr;
    QListView*          tracksList_    = nullptr;
    QStandardItemModel* tracksModel_   = nullptr;
};

} // namespace mf::app::widgets
