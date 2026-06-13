// ArtistView.h
// Full artist overlay. Reachable via NavigationService::
//   artistNavigationRequested(ArtistInfo).
//
//   ┌──────────────────────────────────────────────────────┐
//   │ [← Back]                                             │
//   │                                                      │
//   │ ┌────────┐  Artist Name                              │
//   │ │  ♪     │  Description text spanning several lines   │
//   │ │ 96x96  │  12,345 subscribers                       │
//   │ └────────┘  ⓕ Follow                                 │
//   │                                                      │
//   │ Top tracks                                           │
//   │   1.  Track A — Album X                       3:42   │
//   │   2.  …                                              │
//   │                                                      │
//   │ Albums                                               │
//   │   [card] [card] [card] [card]                        │
//   └──────────────────────────────────────────────────────┘
//
// The widget is a thin view over ArtistViewModel. It binds to
// infoChanged / contentChanged / followedChanged signals for
// re-renders, and routes click handlers through the view model's
// Q_INVOKABLE methods. The view model is responsible for the
// queue work; the widget owns the styling and the list models.

#pragma once

#include "../../core/models/ArtistInfo.h"

#include <QWidget>

class QLabel;
class QListView;
class QPushButton;
class QScrollArea;
class QStandardItemModel;
class QVBoxLayout;

namespace mf::core::playback     { class QueueManager; }
namespace mf::core::services     { class NavigationService; class ImageCache; }
namespace mf::core::theme        { class ThemeManager; }
namespace mf::app::viewmodels    { class ArtistViewModel; }
namespace mf::app::widgets       { class CoverImage; }

namespace mf::app::widgets {

class ArtistView : public QWidget {
    Q_OBJECT
public:
    ArtistView(mf::app::viewmodels::ArtistViewModel* vm,
               mf::core::services::NavigationService* nav,
               mf::core::playback::QueueManager*     queue,
               mf::core::services::ImageCache*       imageCache,
               mf::core::theme::ThemeManager*        theme,
               QWidget* parent = nullptr);
    ~ArtistView() override = default;

public slots:
    void setArtist(const mf::core::models::ArtistInfo& info);

private slots:
    void onBackClicked();
    void onTrackDoubleClicked(const QModelIndex& idx);
    void onAlbumDoubleClicked(const QModelIndex& idx);
    void onFollowClicked();
    void onVmInfoChanged();
    void onVmFollowedChanged();
    void onVmContentChanged();
    void onVmOpenAlbumRequested(const mf::core::models::AlbumInfo& album);

private:
    void buildUi();
    void applyTheme();
    void renderArtist();
    void renderTracksAndAlbums();

    mf::app::viewmodels::ArtistViewModel* vm_           = nullptr;
    mf::core::services::NavigationService* nav_         = nullptr;
    mf::core::playback::QueueManager*     queue_        = nullptr;
    mf::core::services::ImageCache*       imageCache_   = nullptr;
    mf::core::theme::ThemeManager*        theme_        = nullptr;

    QPushButton*        backBtn_        = nullptr;
    CoverImage*         avatar_         = nullptr;
    QLabel*             nameLabel_      = nullptr;
    QLabel*             descLabel_      = nullptr;
    QLabel*             subsLabel_      = nullptr;
    QPushButton*        followBtn_      = nullptr;

    QLabel*             topTracksHeader_ = nullptr;
    QListView*          topTracksList_   = nullptr;
    QStandardItemModel* topTracksModel_  = nullptr;

    QLabel*             albumsHeader_   = nullptr;
    QListView*          albumsList_     = nullptr;
    QStandardItemModel* albumsModel_    = nullptr;
};

} // namespace mf::app::widgets
