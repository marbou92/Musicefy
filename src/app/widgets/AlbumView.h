// AlbumView.h
// Full album overlay. Reachable via NavigationService::
//   albumNavigationRequested(AlbumInfo).
//
// The widget is a thin view over AlbumViewModel. It binds to
// infoChanged / savedChanged signals for re-renders, and routes
// play / shuffle / save clicks through the view model's
// Q_INVOKABLE methods. The view model is responsible for the
// queue work; the widget owns the styling and the track-list
// model.

#pragma once

#include "../../core/models/AlbumInfo.h"

#include <QWidget>

class QLabel;
class QListView;
class QPushButton;
class QStandardItemModel;

namespace mf::core::playback     { class QueueManager; }
namespace mf::core::services     { class NavigationService; class ImageCache; }
namespace mf::core::theme        { class ThemeManager; }
namespace mf::app::viewmodels    { class AlbumViewModel; }
namespace mf::app::widgets       { class CoverImage; }

namespace mf::app::widgets {

class AlbumView : public QWidget {
    Q_OBJECT
public:
    AlbumView(mf::app::viewmodels::AlbumViewModel* vm,
              mf::core::services::NavigationService* nav,
              mf::core::playback::QueueManager*     queue,
              mf::core::services::ImageCache*       imageCache,
              mf::core::theme::ThemeManager*        theme,
              QWidget* parent = nullptr);
    ~AlbumView() override = default;

public slots:
    void setAlbum(const mf::core::models::AlbumInfo& info);

private slots:
    void onBackClicked();
    void onPlayAllClicked();
    void onShuffleClicked();
    void onSaveToggled(bool checked);
    void onTrackDoubleClicked(const QModelIndex& idx);
    void onShuffleChanged(bool on);
    void onVmInfoChanged();
    void onVmSavedChanged();

private:
    void buildUi();
    void applyTheme();
    void renderAlbum();
    QString formatTotalDuration(std::chrono::seconds s) const;

    mf::app::viewmodels::AlbumViewModel* vm_           = nullptr;
    mf::core::services::NavigationService* nav_        = nullptr;
    mf::core::playback::QueueManager*     queue_       = nullptr;
    mf::core::services::ImageCache*       imageCache_  = nullptr;
    mf::core::theme::ThemeManager*        theme_       = nullptr;

    QPushButton*        backBtn_       = nullptr;
    QPushButton*        playAllBtn_    = nullptr;
    QPushButton*        shuffleBtn_    = nullptr;
    QPushButton*        saveBtn_       = nullptr;
    CoverImage*         coverLabel_    = nullptr;
    QLabel*             titleLabel_    = nullptr;
    QLabel*             subtitleLabel_ = nullptr;
    QListView*          tracksList_    = nullptr;
    QStandardItemModel* tracksModel_   = nullptr;
};

} // namespace mf::app::widgets
