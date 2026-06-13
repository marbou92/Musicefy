// LibraryView.cpp
// See header.

#include "LibraryView.h"

#include "viewmodels/LibraryViewModel.h"

#include "../core/models/AlbumInfo.h"
#include "../core/models/ArtistInfo.h"
#include "../core/models/MusicFile.h"
#include "../core/models/PlaylistInfo.h"
#include "../core/services/NavigationService.h"
#include "../core/theme/MusicefyColorScheme.h"
#include "../core/theme/ThemeManager.h"

#include <QFont>
#include <QHBoxLayout>
#include <QLabel>
#include <QListView>
#include <QPushButton>
#include <QStandardItem>
#include <QStandardItemModel>
#include <QTabWidget>
#include <QVBoxLayout>
#include <algorithm>

namespace mf::app::widgets {

using mf::app::viewmodels::LibraryViewModel;
using mf::core::models::AlbumInfo;
using mf::core::models::ArtistInfo;
using mf::core::models::MusicFile;
using mf::core::models::PlaylistInfo;
using mf::core::services::NavigationService;
using mf::core::theme::ThemeManager;
using mf::core::theme::MusicefyColorScheme;

namespace {
QString formatDuration(std::chrono::seconds s) {
    qint64 total = s.count();
    if (total <= 0) return QStringLiteral("--:--");
    return QStringLiteral("%1:%2")
        .arg(total / 60)
        .arg(total % 60, 2, 10, QLatin1Char('0'));
}
}

LibraryView::LibraryView(LibraryViewModel* vm,
                         NavigationService* nav,
                         ThemeManager*     theme,
                         QWidget*          parent)
    : QWidget(parent)
    , vm_(vm)
    , nav_(nav)
    , theme_(theme)
{
    buildUi();
    applyTheme();
    populateTracks();
    populateArtists();
    populateAlbums();
    populatePlaylists();

    if (vm_) {
        connect(vm_, &LibraryViewModel::tracksChanged,
                this, &LibraryView::onTracksChanged);
        connect(vm_, &LibraryViewModel::artistsChanged,
                this, &LibraryView::onArtistsChanged);
        connect(vm_, &LibraryViewModel::albumsChanged,
                this, &LibraryView::onAlbumsChanged);
        connect(vm_, &LibraryViewModel::playlistsChanged,
                this, &LibraryView::onPlaylistsChanged);
        connect(vm_, &LibraryViewModel::libraryChanged,
                this, &LibraryView::onLibraryChanged);
    }
    if (theme_) {
        connect(theme_, &ThemeManager::schemeChanged,
                this, &LibraryView::onThemeChanged);
    }
}

void LibraryView::buildUi() {
    auto* root = new QVBoxLayout(this);
    root->setContentsMargins(0, 0, 0, 0);
    root->setSpacing(0);

    // ── Header ────────────────────────────────────────────────────
    auto* headerWrap = new QWidget(this);
    auto* hl = new QHBoxLayout(headerWrap);
    hl->setContentsMargins(28, 24, 28, 8);
    hl->setSpacing(12);

    header_ = new QLabel(QStringLiteral("Library"), headerWrap);
    QFont hf = header_->font();
    hf.setPointSize(20);
    hf.setBold(true);
    header_->setFont(hf);
    hl->addWidget(header_);
    hl->addStretch(1);

    auto* playAll = new QPushButton(QStringLiteral("Play all"), headerWrap);
    playAll->setCursor(Qt::PointingHandCursor);
    connect(playAll, &QPushButton::clicked,
            this, &LibraryView::onPlayAllClicked);
    hl->addWidget(playAll);

    root->addWidget(headerWrap);

    // ── Tabs ──────────────────────────────────────────────────────
    tabs_ = new QTabWidget(this);
    tabs_->setDocumentMode(true);
    tabs_->setTabPosition(QTabWidget::North);
    root->addWidget(tabs_, /*stretch=*/1);

    // Tracks tab ──────────────────────────────────────────────────
    auto* tracksPage = new QWidget(tabs_);
    auto* tl = new QVBoxLayout(tracksPage);
    tl->setContentsMargins(20, 12, 20, 20);
    tl->setSpacing(8);

    tracksEmpty_ = new QLabel(
        QStringLiteral("No tracks yet. Add a music folder in Settings → Library / Folders to start."),
        tracksPage);
    tracksEmpty_->setAlignment(Qt::AlignCenter);
    tracksEmpty_->setWordWrap(true);
    tracksEmpty_->setProperty("role", QStringLiteral("secondary"));
    tl->addWidget(tracksEmpty_);

    tracksList_  = new QListView(tracksPage);
    tracksModel_ = new QStandardItemModel(this);
    tracksList_->setModel(tracksModel_);
    tracksList_->setUniformItemSizes(true);
    tracksList_->setEditTriggers(QAbstractItemView::NoEditTriggers);
    tracksList_->setSelectionMode(QAbstractItemView::SingleSelection);
    tracksList_->setAlternatingRowColors(true);
    connect(tracksList_, &QListView::doubleClicked,
            this, &LibraryView::onTrackDoubleClicked);
    tl->addWidget(tracksList_, /*stretch=*/1);
    tabs_->addTab(tracksPage, QStringLiteral("Tracks"));

    // Artists tab ─────────────────────────────────────────────────
    auto* artistsPage = new QWidget(tabs_);
    auto* al = new QVBoxLayout(artistsPage);
    al->setContentsMargins(20, 12, 20, 20);
    al->setSpacing(8);

    artistsEmpty_ = new QLabel(
        QStringLiteral("No artists yet. They'll appear here as your library grows."),
        artistsPage);
    artistsEmpty_->setAlignment(Qt::AlignCenter);
    artistsEmpty_->setWordWrap(true);
    artistsEmpty_->setProperty("role", QStringLiteral("secondary"));
    al->addWidget(artistsEmpty_);

    artistsList_  = new QListView(artistsPage);
    artistsModel_ = new QStandardItemModel(this);
    artistsList_->setModel(artistsModel_);
    artistsList_->setUniformItemSizes(true);
    artistsList_->setEditTriggers(QAbstractItemView::NoEditTriggers);
    artistsList_->setSelectionMode(QAbstractItemView::SingleSelection);
    connect(artistsList_, &QListView::doubleClicked,
            this, &LibraryView::onArtistDoubleClicked);
    al->addWidget(artistsList_, /*stretch=*/1);
    tabs_->addTab(artistsPage, QStringLiteral("Artists"));

    // Albums tab ──────────────────────────────────────────────────
    auto* albumsPage = new QWidget(tabs_);
    auto* abl = new QVBoxLayout(albumsPage);
    abl->setContentsMargins(20, 12, 20, 20);
    abl->setSpacing(8);

    albumsEmpty_ = new QLabel(
        QStringLiteral("No albums yet."),
        albumsPage);
    albumsEmpty_->setAlignment(Qt::AlignCenter);
    albumsEmpty_->setWordWrap(true);
    albumsEmpty_->setProperty("role", QStringLiteral("secondary"));
    abl->addWidget(albumsEmpty_);

    albumsList_  = new QListView(albumsPage);
    albumsModel_ = new QStandardItemModel(this);
    albumsList_->setModel(albumsModel_);
    albumsList_->setUniformItemSizes(true);
    albumsList_->setEditTriggers(QAbstractItemView::NoEditTriggers);
    albumsList_->setSelectionMode(QAbstractItemView::SingleSelection);
    connect(albumsList_, &QListView::doubleClicked,
            this, &LibraryView::onAlbumDoubleClicked);
    abl->addWidget(albumsList_, /*stretch=*/1);
    tabs_->addTab(albumsPage, QStringLiteral("Albums"));

    // Playlists tab ───────────────────────────────────────────────
    auto* playlistsPage = new QWidget(tabs_);
    auto* pl = new QVBoxLayout(playlistsPage);
    pl->setContentsMargins(20, 12, 20, 20);
    pl->setSpacing(8);

    playlistsEmpty_ = new QLabel(
        QStringLiteral("No playlists yet. Create one with the + button on a track or album."),
        playlistsPage);
    playlistsEmpty_->setAlignment(Qt::AlignCenter);
    playlistsEmpty_->setWordWrap(true);
    playlistsEmpty_->setProperty("role", QStringLiteral("secondary"));
    pl->addWidget(playlistsEmpty_);

    playlistsList_  = new QListView(playlistsPage);
    playlistsModel_ = new QStandardItemModel(this);
    playlistsList_->setModel(playlistsModel_);
    playlistsList_->setUniformItemSizes(true);
    playlistsList_->setEditTriggers(QAbstractItemView::NoEditTriggers);
    playlistsList_->setSelectionMode(QAbstractItemView::SingleSelection);
    connect(playlistsList_, &QListView::doubleClicked,
            this, &LibraryView::onPlaylistDoubleClicked);
    pl->addWidget(playlistsList_, /*stretch=*/1);
    tabs_->addTab(playlistsPage, QStringLiteral("Playlists"));

    // Most Played tab
    auto* mostPlayedPage = new QWidget(tabs_);
    auto* mpl = new QVBoxLayout(mostPlayedPage);
    mpl->setContentsMargins(20, 12, 20, 20);
    mpl->setSpacing(8);

    mostPlayedEmpty_ = new QLabel(
        QStringLiteral("No played tracks yet. Play some music to see your most played here."),
        mostPlayedPage);
    mostPlayedEmpty_->setAlignment(Qt::AlignCenter);
    mostPlayedEmpty_->setWordWrap(true);
    mostPlayedEmpty_->setProperty("role", QStringLiteral("secondary"));
    mpl->addWidget(mostPlayedEmpty_);

    mostPlayedList_  = new QListView(mostPlayedPage);
    mostPlayedModel_ = new QStandardItemModel(this);
    mostPlayedList_->setModel(mostPlayedModel_);
    mostPlayedList_->setUniformItemSizes(true);
    mostPlayedList_->setEditTriggers(QAbstractItemView::NoEditTriggers);
    mostPlayedList_->setSelectionMode(QAbstractItemView::SingleSelection);
    mostPlayedList_->setAlternatingRowColors(true);
    connect(mostPlayedList_, &QListView::doubleClicked,
            this, &LibraryView::onMostPlayedDoubleClicked);
    mpl->addWidget(mostPlayedList_, /*stretch=*/1);
    tabs_->addTab(mostPlayedPage, QStringLiteral("Most Played"));

    // Recently Added tab
    auto* recentAddedPage = new QWidget(tabs_);
    auto* ral = new QVBoxLayout(recentAddedPage);
    ral->setContentsMargins(20, 12, 20, 20);
    ral->setSpacing(8);

    recentAddedEmpty_ = new QLabel(
        QStringLiteral("No recently added tracks. Add new music to see it here."),
        recentAddedPage);
    recentAddedEmpty_->setAlignment(Qt::AlignCenter);
    recentAddedEmpty_->setWordWrap(true);
    recentAddedEmpty_->setProperty("role", QStringLiteral("secondary"));
    ral->addWidget(recentAddedEmpty_);

    recentAddedList_  = new QListView(recentAddedPage);
    recentAddedModel_ = new QStandardItemModel(this);
    recentAddedList_->setModel(recentAddedModel_);
    recentAddedList_->setUniformItemSizes(true);
    recentAddedList_->setEditTriggers(QAbstractItemView::NoEditTriggers);
    recentAddedList_->setSelectionMode(QAbstractItemView::SingleSelection);
    recentAddedList_->setAlternatingRowColors(true);
    connect(recentAddedList_, &QListView::doubleClicked,
            this, &LibraryView::onRecentAddedDoubleClicked);
    ral->addWidget(recentAddedList_, /*stretch=*/1);
    tabs_->addTab(recentAddedPage, QStringLiteral("Recently Added"));
}

void LibraryView::applyTheme() {
    MusicefyColorScheme s;
    if (theme_) s = theme_->scheme();
    if (!s.primary.isValid()) {
        setStyleSheet(QString());
        return;
    }
    setStyleSheet(QStringLiteral(
        "QWidget { background: transparent; color: %1; }"
        "QLabel[role=\"secondary\"] { color: %2; }"
        "QTabWidget::pane { border: 1px solid %3; border-radius: 8px;"
        "  background: %4; }"
        "QTabBar::tab { background: %5; color: %1; padding: 6px 14px;"
        "  border: 1px solid %3; border-bottom: none;"
        "  border-top-left-radius: 6px; border-top-right-radius: 6px;"
        "  margin-right: 2px; }"
        "QTabBar::tab:selected { background: %4; color: %6; }"
        "QListView { background: %4; color: %1;"
        "  border: none; border-radius: 0 0 6px 6px;"
        "  selection-background-color: %7; selection-color: %8;"
        "  alternate-background-color: %9; }"
        "QHeaderView::section { background: %5; color: %2; padding: 6px;"
        "  border: none; border-bottom: 1px solid %3; }"
        "QPushButton { background: %5; color: %1;"
        "  border: 1px solid %3; border-radius: 6px; padding: 6px 14px; }"
        "QPushButton:hover { background: %10; }"
    )
    .arg(s.onSurface.name())
    .arg(s.onSurfaceVariant.name())
    .arg(s.outlineVariant.name())
    .arg(s.surfaceContainer.name())
    .arg(s.surfaceContainerHigh.name())
    .arg(s.onSurface.name())                 // selected tab text (uses s.onSurface over surfaceContainer)
    .arg(s.primaryContainer.name())
    .arg(s.onPrimaryContainer.name())
    .arg(s.surfaceContainerLow.name())
    .arg(s.surfaceContainerHighest.name())
    );
}

void LibraryView::onThemeChanged() {
    applyTheme();
}

void LibraryView::onLibraryChanged() {
    populateTracks();
    populateArtists();
    populateAlbums();
    populatePlaylists();
    populateMostPlayed();
    populateRecentAdded();
}

void LibraryView::onTracksChanged()    { populateTracks(); }
void LibraryView::onArtistsChanged()   { populateArtists(); }
void LibraryView::onAlbumsChanged()    { populateAlbums(); }
void LibraryView::onPlaylistsChanged() { populatePlaylists(); }

void LibraryView::populateTracks() {
    tracksModel_->clear();
    tracksModel_->setHorizontalHeaderLabels(
        {QStringLiteral("Title"), QStringLiteral("Artist"),
         QStringLiteral("Album"), QStringLiteral("Duration")});

    if (!vm_) {
        tracksList_->setVisible(false);
        tracksEmpty_->setVisible(true);
        return;
    }
    const auto tracks = vm_->tracks();
    if (tracks.isEmpty()) {
        tracksList_->setVisible(false);
        tracksEmpty_->setVisible(true);
        return;
    }
    tracksList_->setVisible(true);
    tracksEmpty_->setVisible(false);

    for (const auto& t : tracks) {
        tracksModel_->appendRow({
            new QStandardItem(t.title()),
            new QStandardItem(t.artist()),
            new QStandardItem(t.album()),
            new QStandardItem(formatDuration(t.duration())),
        });
    }
}

void LibraryView::populateArtists() {
    artistsModel_->clear();
    artistsModel_->setHorizontalHeaderLabels(
        {QStringLiteral("Artist"), QStringLiteral("Tracks")});

    if (!vm_) {
        artistsList_->setVisible(false);
        artistsEmpty_->setVisible(true);
        return;
    }
    const auto artists = vm_->artists();
    if (artists.isEmpty()) {
        artistsList_->setVisible(false);
        artistsEmpty_->setVisible(true);
        return;
    }
    artistsList_->setVisible(true);
    artistsEmpty_->setVisible(false);

    // Per-artist track count: walk all tracks once and bucket by
    // artist name. (The schema has both `artist` text and `artist_id`;
    // for the tab counter we use the name since it's what the user
    // sees.)
    const auto tracks = vm_->tracks();
    QHash<QString, int> counts;
    for (const auto& t : tracks) {
        ++counts[t.artist()];
    }

    for (const auto& a : artists) {
        const int n = counts.value(a.name(), 0);
        artistsModel_->appendRow({
            new QStandardItem(a.name()),
            new QStandardItem(QString::number(n)),
        });
    }
}

void LibraryView::populateAlbums() {
    albumsModel_->clear();
    albumsModel_->setHorizontalHeaderLabels(
        {QStringLiteral("Album"), QStringLiteral("Artist"),
         QStringLiteral("Year"), QStringLiteral("Tracks")});

    if (!vm_) {
        albumsList_->setVisible(false);
        albumsEmpty_->setVisible(true);
        return;
    }
    const auto albums = vm_->albums();
    if (albums.isEmpty()) {
        albumsList_->setVisible(false);
        albumsEmpty_->setVisible(true);
        return;
    }
    albumsList_->setVisible(true);
    albumsEmpty_->setVisible(false);

    for (const auto& a : albums) {
        albumsModel_->appendRow({
            new QStandardItem(a.name()),
            new QStandardItem(a.artist()),
            new QStandardItem(a.year() > 0 ? QString::number(a.year()) : QString()),
            new QStandardItem(QString::number(a.trackCount())),
        });
    }
}

void LibraryView::populatePlaylists() {
    playlistsModel_->clear();
    playlistsModel_->setHorizontalHeaderLabels(
        {QStringLiteral("Playlist"), QStringLiteral("Tracks")});

    if (!vm_) {
        playlistsList_->setVisible(false);
        playlistsEmpty_->setVisible(true);
        return;
    }
    const auto pls = vm_->playlists();
    if (pls.isEmpty()) {
        playlistsList_->setVisible(false);
        playlistsEmpty_->setVisible(true);
        return;
    }
    playlistsList_->setVisible(true);
    playlistsEmpty_->setVisible(false);

    for (const auto& p : pls) {
        playlistsModel_->appendRow({
            new QStandardItem(p.name()),
            new QStandardItem(QString::number(p.trackCount())),
        });
    }
}

void LibraryView::onTrackDoubleClicked(const QModelIndex& proxyIdx) {
    if (!vm_ || !proxyIdx.isValid()) return;
    int row = proxyIdx.row();
    auto t = vm_->trackAt(row);
    if (t.filePath().isEmpty()) return;
    vm_->playTrack(t.filePath());
}

void LibraryView::onArtistDoubleClicked(const QModelIndex& proxyIdx) {
    if (!vm_ || !nav_ || !proxyIdx.isValid()) return;
    const auto artists = vm_->artists();
    if (proxyIdx.row() < 0 || proxyIdx.row() >= artists.size()) return;
    nav_->requestArtist(artists[proxyIdx.row()]);
}

void LibraryView::onAlbumDoubleClicked(const QModelIndex& proxyIdx) {
    if (!vm_ || !nav_ || !proxyIdx.isValid()) return;
    const auto albums = vm_->albums();
    if (proxyIdx.row() < 0 || proxyIdx.row() >= albums.size()) return;
    nav_->requestAlbum(albums[proxyIdx.row()]);
}

void LibraryView::onPlaylistDoubleClicked(const QModelIndex& proxyIdx) {
    if (!vm_ || !nav_ || !proxyIdx.isValid()) return;
    const auto pls = vm_->playlists();
    if (proxyIdx.row() < 0 || proxyIdx.row() >= pls.size()) return;
    nav_->requestPlaylist(pls[proxyIdx.row()]);
}

void LibraryView::onPlayAllClicked() {
    if (vm_) vm_->playAll();
}

void LibraryView::populateMostPlayed() {
    mostPlayedModel_->clear();
    mostPlayedModel_->setHorizontalHeaderLabels(
        {QStringLiteral("Title"), QStringLiteral("Artist"),
         QStringLiteral("Plays")});

    if (!vm_) {
        mostPlayedList_->setVisible(false);
        mostPlayedEmpty_->setVisible(true);
        return;
    }
    const auto tracks = vm_->tracks();
    QList<MusicFile> played;
    for (const auto& t : tracks) {
        if (t.playCount() > 0) played.append(t);
    }
    std::sort(played.begin(), played.end(),
        [](const MusicFile& a, const MusicFile& b) {
            return a.playCount() > b.playCount();
        });

    if (played.isEmpty()) {
        mostPlayedList_->setVisible(false);
        mostPlayedEmpty_->setVisible(true);
        return;
    }
    mostPlayedList_->setVisible(true);
    mostPlayedEmpty_->setVisible(false);

    for (const auto& t : played) {
        mostPlayedModel_->appendRow({
            new QStandardItem(t.title()),
            new QStandardItem(t.artist()),
            new QStandardItem(QString::number(t.playCount())),
        });
    }
}

void LibraryView::populateRecentAdded() {
    recentAddedModel_->clear();
    recentAddedModel_->setHorizontalHeaderLabels(
        {QStringLiteral("Title"), QStringLiteral("Artist"),
         QStringLiteral("Date Added")});

    if (!vm_) {
        recentAddedList_->setVisible(false);
        recentAddedEmpty_->setVisible(true);
        return;
    }
    const auto tracks = vm_->tracks();
    QList<MusicFile> withDate;
    for (const auto& t : tracks) {
        if (t.dateAdded().has_value()) withDate.append(t);
    }
    std::sort(withDate.begin(), withDate.end(),
        [](const MusicFile& a, const MusicFile& b) {
            return a.dateAdded().value_or(QDateTime()) > b.dateAdded().value_or(QDateTime());
        });

    if (withDate.isEmpty()) {
        recentAddedList_->setVisible(false);
        recentAddedEmpty_->setVisible(true);
        return;
    }
    recentAddedList_->setVisible(true);
    recentAddedEmpty_->setVisible(false);

    for (const auto& t : withDate) {
        QString dateStr = t.dateAdded()->toString(QStringLiteral("yyyy-MM-dd"));
        recentAddedModel_->appendRow({
            new QStandardItem(t.title()),
            new QStandardItem(t.artist()),
            new QStandardItem(dateStr),
        });
    }
}

void LibraryView::onMostPlayedDoubleClicked(const QModelIndex& proxyIdx) {
    if (!vm_ || !proxyIdx.isValid()) return;
    const auto tracks = vm_->tracks();
    QList<MusicFile> played;
    for (const auto& t : tracks) {
        if (t.playCount() > 0) played.append(t);
    }
    std::sort(played.begin(), played.end(),
        [](const MusicFile& a, const MusicFile& b) {
            return a.playCount() > b.playCount();
        });
    int row = proxyIdx.row();
    if (row < 0 || row >= played.size()) return;
    auto t = played[row];
    if (t.filePath().isEmpty()) return;
    vm_->playTrack(t.filePath());
}

void LibraryView::onRecentAddedDoubleClicked(const QModelIndex& proxyIdx) {
    if (!vm_ || !proxyIdx.isValid()) return;
    const auto tracks = vm_->tracks();
    QList<MusicFile> withDate;
    for (const auto& t : tracks) {
        if (t.dateAdded().has_value()) withDate.append(t);
    }
    std::sort(withDate.begin(), withDate.end(),
        [](const MusicFile& a, const MusicFile& b) {
            return a.dateAdded().value_or(QDateTime()) > b.dateAdded().value_or(QDateTime());
        });
    int row = proxyIdx.row();
    if (row < 0 || row >= withDate.size()) return;
    auto t = withDate[row];
    if (t.filePath().isEmpty()) return;
    vm_->playTrack(t.filePath());
}

} // namespace mf::app::widgets
