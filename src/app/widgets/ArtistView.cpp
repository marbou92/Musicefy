// ArtistView.cpp
// See header. The widget binds to ArtistViewModel and renders the
// artist. All queue work is delegated to the view model; the
// widget only handles styling, layout, and signal routing.

#include "ArtistView.h"

#include "CoverImage.h"
#include "SvgIcon.h"

#include "../core/models/AlbumInfo.h"
#include "../core/models/MusicFile.h"
#include "../core/playback/QueueManager.h"
#include "../core/services/ImageCache.h"
#include "../core/services/NavigationService.h"
#include "../core/theme/MusicefyColorScheme.h"
#include "../core/theme/ThemeManager.h"
#include "../viewmodels/ArtistViewModel.h"

#include <QFont>
#include <QHBoxLayout>
#include <QLabel>
#include <QListView>
#include <QPushButton>
#include <QScrollArea>
#include <QStandardItem>
#include <QStandardItemModel>
#include <QVBoxLayout>

namespace mf::app::widgets {

using mf::core::models::AlbumInfo;
using mf::core::models::ArtistInfo;
using mf::core::models::MusicFile;
using mf::core::playback::QueueManager;
using mf::core::services::ImageCache;
using mf::core::services::NavigationService;
using mf::core::theme::ThemeManager;
using mf::core::theme::MusicefyColorScheme;
using mf::app::viewmodels::ArtistViewModel;

namespace {
QString formatDuration(std::chrono::seconds s) {
    qint64 total = s.count();
    if (total <= 0) return QStringLiteral("--:--");
    return QStringLiteral("%1:%2")
        .arg(total / 60)
        .arg(total % 60, 2, 10, QLatin1Char('0'));
}
QString formatSubscribers(qint64 n) {
    QString formatted;
    if (n >= 1'000'000) {
        formatted = QStringLiteral("%1M").arg(n / 1'000'000.0, 0, 'f', 1);
    } else if (n >= 1'000) {
        formatted = QStringLiteral("%1K").arg(n / 1'000.0, 0, 'f', 1);
    } else {
        formatted = QString::number(n);
    }
    return QStringLiteral("%1 subscribers").arg(formatted);
}
} // anonymous namespace

ArtistView::ArtistView(ArtistViewModel* vm,
                       NavigationService* nav,
                       QueueManager*     queue,
                       ImageCache*       imageCache,
                       ThemeManager*     theme,
                       QWidget*          parent)
    : QWidget(parent)
    , vm_(vm)
    , nav_(nav)
    , queue_(queue)
    , imageCache_(imageCache)
    , theme_(theme)
{
    buildUi();
    applyTheme();
    if (theme_) {
        connect(theme_, &ThemeManager::schemeChanged,
                this, [this]() {
                    applyTheme();
                    if (avatar_) avatar_->refreshPlaceholder();
                });
    }
    if (vm_) {
        connect(vm_, &ArtistViewModel::infoChanged,
                this, &ArtistView::onVmInfoChanged);
        connect(vm_, &ArtistViewModel::followedChanged,
                this, &ArtistView::onVmFollowedChanged);
        connect(vm_, &ArtistViewModel::contentChanged,
                this, &ArtistView::onVmContentChanged);
        connect(vm_, &ArtistViewModel::openAlbumRequested,
                this, &ArtistView::onVmOpenAlbumRequested);
        renderArtist();
        renderTracksAndAlbums();
    }
}

void ArtistView::buildUi() {
    auto* root = new QVBoxLayout(this);
    root->setContentsMargins(0, 0, 0, 0);
    root->setSpacing(0);

    auto* scroller = new QScrollArea(this);
    scroller->setWidgetResizable(true);
    scroller->setFrameShape(QFrame::NoFrame);
    root->addWidget(scroller, /*stretch=*/1);

    auto* content = new QWidget(scroller);
    auto* col = new QVBoxLayout(content);
    col->setContentsMargins(28, 18, 28, 28);
    col->setSpacing(16);

    auto* backRow = new QHBoxLayout();
    backRow->setContentsMargins(0, 0, 0, 0);
    backRow->setSpacing(0);
    backBtn_ = new QPushButton(content);
    backBtn_->setText(QStringLiteral("Back"));
    backBtn_->setCursor(Qt::PointingHandCursor);
    backBtn_->setFlat(true);
    backBtn_->setIconSize(QSize(18, 18));
    backBtn_->setProperty("role", QStringLiteral("backButton"));
    connect(backBtn_, &QPushButton::clicked,
            this, &ArtistView::onBackClicked);
    backRow->addWidget(backBtn_);
    backRow->addStretch(1);
    col->addLayout(backRow);

    auto* headerRow = new QHBoxLayout();
    headerRow->setSpacing(20);

    avatar_ = new CoverImage(imageCache_, content);
    avatar_->setFixedSize(120, 120);
    headerRow->addWidget(avatar_);

    auto* headerText = new QVBoxLayout();
    headerText->setSpacing(4);

    nameLabel_ = new QLabel(content);
    QFont nf = nameLabel_->font();
    nf.setPointSize(22);
    nf.setBold(true);
    nameLabel_->setFont(nf);
    nameLabel_->setWordWrap(true);
    headerText->addWidget(nameLabel_);

    descLabel_ = new QLabel(content);
    descLabel_->setWordWrap(true);
    descLabel_->setProperty("role", QStringLiteral("secondary"));
    headerText->addWidget(descLabel_);

    subsLabel_ = new QLabel(content);
    subsLabel_->setProperty("role", QStringLiteral("secondary"));
    headerText->addWidget(subsLabel_);

    headerText->addStretch(1);
    headerRow->addLayout(headerText, /*stretch=*/1);

    followBtn_ = new QPushButton(content);
    followBtn_->setText(QStringLiteral("  Follow"));
    followBtn_->setCursor(Qt::PointingHandCursor);
    followBtn_->setIconSize(QSize(16, 16));
    connect(followBtn_, &QPushButton::clicked,
            this, &ArtistView::onFollowClicked);
    headerRow->addWidget(followBtn_, /*stretch=*/0, Qt::AlignTop);

    col->addLayout(headerRow);

    topTracksHeader_ = new QLabel(QStringLiteral("Top tracks"), content);
    QFont ttf = topTracksHeader_->font();
    ttf.setPointSize(14);
    ttf.setBold(true);
    topTracksHeader_->setFont(ttf);
    col->addWidget(topTracksHeader_);

    topTracksList_  = new QListView(content);
    topTracksModel_ = new QStandardItemModel(this);
    topTracksList_->setModel(topTracksModel_);
    topTracksList_->setUniformItemSizes(true);
    topTracksList_->setEditTriggers(QAbstractItemView::NoEditTriggers);
    topTracksList_->setSelectionMode(QAbstractItemView::SingleSelection);
    topTracksList_->setAlternatingRowColors(true);
    connect(topTracksList_, &QListView::doubleClicked,
            this, &ArtistView::onTrackDoubleClicked);
    col->addWidget(topTracksList_);

    albumsHeader_ = new QLabel(QStringLiteral("Albums"), content);
    QFont ahf = albumsHeader_->font();
    ahf.setPointSize(14);
    ahf.setBold(true);
    albumsHeader_->setFont(ahf);
    col->addWidget(albumsHeader_);

    albumsList_  = new QListView(content);
    albumsModel_ = new QStandardItemModel(this);
    albumsList_->setModel(albumsModel_);
    albumsList_->setUniformItemSizes(true);
    albumsList_->setEditTriggers(QAbstractItemView::NoEditTriggers);
    albumsList_->setSelectionMode(QAbstractItemView::SingleSelection);
    albumsList_->setAlternatingRowColors(true);
    connect(albumsList_, &QListView::doubleClicked,
            this, &ArtistView::onAlbumDoubleClicked);
    col->addWidget(albumsList_);

    scroller->setWidget(content);
}

void ArtistView::applyTheme() {
    MusicefyColorScheme s;
    if (theme_) s = theme_->scheme();
    if (!s.primary.isValid()) {
        setStyleSheet(QString());
        return;
    }
    setStyleSheet(QStringLiteral(
        "QWidget { background: %1; color: %2; }"
        "QScrollArea { background: %1; border: none; }"
        "QPushButton[role=\"backButton\"] {"
        "  background: transparent; color: %3; text-align: left;"
        "  border: none; padding: 4px 8px; }"
        "QPushButton[role=\"backButton\"]:hover { color: %4; }"
        "QLabel[role=\"secondary\"] { color: %5; }"
        "QLabel[role=\"avatar\"] {"
        "  background: %6; color: %4; border-radius: 8px;"
        "  border: 1px solid %7; }"
        "QPushButton { background: %6; color: %2;"
        "  border: 1px solid %7; border-radius: 6px; padding: 6px 14px; }"
        "QPushButton:hover { background: %8; }"
        "QListView { background: %6; color: %2;"
        "  border: 1px solid %7; border-radius: 6px;"
        "  selection-background-color: %4; selection-color: %9;"
        "  alternate-background-color: %10; }"
    )
    .arg(s.surface.name())                 // 1 widget background
    .arg(s.onSurface.name())               // 2 default text
    .arg(s.onSurfaceVariant.name())        // 3 back button
    .arg(s.primary.name())                 // 4 hover/selection
    .arg(s.onSurfaceVariant.name())        // 5 secondary text
    .arg(s.surfaceContainerHigh.name())    // 6 list/avatar bg
    .arg(s.outlineVariant.name())          // 7 borders
    .arg(s.surfaceContainerHighest.name()) // 8 button hover
    .arg(s.onPrimary.name())               // 9 selection text
    .arg(s.surfaceContainer.name())        // 10 alt row
    );

    // Re-render SVG icons with the current theme colors.
    if (backBtn_) {
        backBtn_->setIcon(SvgIcon::get("arrow-left", s.onSurfaceVariant, 18));
    }
    if (followBtn_) {
        const bool followed = vm_ && vm_->isFollowed();
        followBtn_->setIcon(SvgIcon::get(
            followed ? "heart" : "plus",
            s.onSurface, 16));
    }
}

void ArtistView::setArtist(const ArtistInfo& info) {
    if (vm_) vm_->setInfo(info);
}

void ArtistView::onVmInfoChanged() {
    renderArtist();
}

void ArtistView::onVmFollowedChanged() {
    if (!vm_) return;
    if (followBtn_) followBtn_->setText(vm_->isFollowed()
        ? QStringLiteral("  Following")
        : QStringLiteral("  Follow"));
    // Refresh the heart/plus icon to match the new followed state.
    applyTheme();
}

void ArtistView::onVmContentChanged() {
    renderTracksAndAlbums();
}

void ArtistView::onVmOpenAlbumRequested(const AlbumInfo& album) {
    if (nav_) nav_->requestAlbum(album);
}

void ArtistView::renderArtist() {
    if (!vm_) return;
    const ArtistInfo& info = vm_->info();
    nameLabel_->setText(info.name());
    if (avatar_) {
        avatar_->setPlaceholderText(info.name());
    }
    descLabel_->setText(info.description().isEmpty()
        ? QStringLiteral("(No description available.)")
        : info.description());
    if (info.subscriberCount().has_value()) {
        subsLabel_->setText(formatSubscribers(*info.subscriberCount()));
    } else {
        subsLabel_->setText(QString());
    }
    if (followBtn_) followBtn_->setText(vm_->isFollowed()
        ? QStringLiteral("  Following")
        : QStringLiteral("  Follow"));
    // The follow button icon was re-rendered in applyTheme() (called
    // by the ctor) but we also need it refreshed on later state
    // changes — applyTheme() handles that for us, but we still need
    // the initial render to use the current isFollowed() value.
}

void ArtistView::renderTracksAndAlbums() {
    if (!vm_) return;
    topTracksModel_->clear();
    topTracksModel_->setHorizontalHeaderLabels(
        {QStringLiteral("#"), QStringLiteral("Title"),
         QStringLiteral("Album"), QStringLiteral("Duration")});
    int n = 0;
    for (const auto& t : vm_->topTracks()) {
        ++n;
        topTracksModel_->appendRow({
            new QStandardItem(QString::number(n)),
            new QStandardItem(t.title()),
            new QStandardItem(t.album()),
            new QStandardItem(formatDuration(t.duration())),
        });
    }
    if (topTracksHeader_) {
        topTracksHeader_->setText(
            QStringLiteral("Top tracks (%1)").arg(n));
    }

    albumsModel_->clear();
    albumsModel_->setHorizontalHeaderLabels(
        {QStringLiteral("Album"), QStringLiteral("Year"), QStringLiteral("Tracks")});
    int m = 0;
    for (const auto& a : vm_->albums()) {
        ++m;
        albumsModel_->appendRow({
            new QStandardItem(a.title()),
            new QStandardItem(a.year() > 0 ? QString::number(a.year()) : QString()),
            new QStandardItem(QString()),
        });
    }
    if (albumsHeader_) {
        albumsHeader_->setText(
            QStringLiteral("Albums (%1)").arg(m));
    }
}

void ArtistView::onBackClicked() {
    if (nav_) nav_->closeOverlay();
}

void ArtistView::onFollowClicked() {
    if (!vm_) return;
    vm_->toggleFollowed();
}

void ArtistView::onTrackDoubleClicked(const QModelIndex& idx) {
    if (!idx.isValid() || !vm_ || !queue_) return;
    int row = idx.row();
    if (row < 0 || row >= vm_->topTracks().size()) return;
    const auto& t = vm_->topTracks().at(row);
    if (t.filePath().isEmpty()) return;
    queue_->enqueue(t);
    queue_->setCurrentIndex(queue_->count() - 1);
}

void ArtistView::onAlbumDoubleClicked(const QModelIndex& idx) {
    if (!vm_ || !idx.isValid()) return;
    vm_->openAlbumAt(idx.row());
}

} // namespace mf::app::widgets
