// HomeView.cpp
// See header.

#include "HomeView.h"

#include "CardColorGlowBehavior.h"
#include "CoverImage.h"
#include "viewmodels/HomeViewModel.h"

#include "../core/models/MusicFile.h"
#include "../core/models/PlaylistInfo.h"
#include "../core/services/ImageCache.h"
#include "../core/services/LibraryService.h"
#include "../core/services/ToastService.h"
#include "../core/theme/MusicefyColorScheme.h"
#include "../core/theme/ThemeManager.h"

#include <QFileDialog>
#include <QFileInfo>
#include <QFont>
#include <QHBoxLayout>
#include <QLabel>
#include <QMouseEvent>
#include <QPushButton>
#include <QScrollArea>
#include <QStackedWidget>
#include <QVBoxLayout>

namespace mf::app::widgets {

using mf::app::viewmodels::HomeViewModel;
using mf::core::models::MusicFile;
using mf::core::models::PlaylistInfo;
using mf::core::services::ImageCache;
using mf::core::services::LibraryService;
using mf::core::services::ToastService;
using mf::core::theme::ThemeManager;
using mf::core::theme::MusicefyColorScheme;

namespace {
constexpr int kCardWidth    = 180;
constexpr int kCardHeight   = 220;
constexpr int kCoverSize    = 168;
constexpr int kCarouselPad  = 12;
constexpr int kCarouselGap  = 12;
}

// â”€â”€ Card: a flat clickable frame with cover + title + subtitle â”€â”€â”€â”€â”€â”€â”€â”€

class Card : public QFrame {
    Q_OBJECT
public:
    Card(QWidget* parent = nullptr) : QFrame(parent) {
        setObjectName(QStringLiteral("Card"));
        setAttribute(Qt::WA_Hover, true);
        setMouseTracking(true);
        setFixedSize(kCardWidth, kCardHeight);
        setCursor(Qt::PointingHandCursor);
    }
    void setCoverColor(const QColor& c) { coverColor_ = c; refresh(); }
    void setTitle(const QString& t)     { title_ = t;     if (titleLabel_) titleLabel_->setText(t); }
    void setSubtitle(const QString& s)  { subtitle_ = s;  if (subLabel_)   subLabel_->setText(s); }
    QString title()     const { return title_; }
    QString subtitle()  const { return subtitle_; }

signals:
    void activated();

protected:
    void mousePressEvent(QMouseEvent* e) override {
        if (e->button() == Qt::LeftButton) emit activated();
        QFrame::mousePressEvent(e);
    }
    void enterEvent(QEvent* e) override {
        hovered_ = true;
        update();
        QFrame::enterEvent(e);
    }
    void leaveEvent(QEvent* e) override {
        hovered_ = false;
        update();
        QFrame::leaveEvent(e);
    }

private:
    void refresh() { update(); }

    QString title_;
    QString subtitle_;
    QColor  coverColor_;
    bool    hovered_ = false;
    QLabel* titleLabel_ = nullptr;
    QLabel* subLabel_   = nullptr;

    friend class HomeView;
};

// â”€â”€ HomeView â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

HomeView::HomeView(HomeViewModel* vm,
                   LibraryService* libSvc,
                   ImageCache*     imageCache,
                   ThemeManager*   theme,
                   QWidget*        parent)
    : QWidget(parent)
    , vm_(vm)
    , libSvc_(libSvc)
    , imageCache_(imageCache)
    , theme_(theme)
{
    setObjectName(QStringLiteral("HomeView"));
    buildUi();
    applyTheme();
    rebuildContent();

    if (vm_) {
        connect(vm_, &HomeViewModel::contentChanged,
                this, &HomeView::onContentChanged);
    }
    if (theme_) {
        connect(theme_, &ThemeManager::schemeChanged,
                this, &HomeView::onThemeChanged);
    }
}

void HomeView::buildUi() {
    auto* root = new QVBoxLayout(this);
    root->setContentsMargins(0, 0, 0, 0);
    root->setSpacing(0);

    stack_ = new QStackedWidget(this);

    // â”€â”€ Content page â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    contentPage_ = new QWidget(stack_);
    contentBody_ = new QVBoxLayout(contentPage_);
    contentBody_->setContentsMargins(28, 24, 28, 24);
    contentBody_->setSpacing(20);

    greeting_ = new QLabel(contentPage_);
    QFont gf = greeting_->font();
    gf.setPointSize(26);
    gf.setBold(true);
    greeting_->setFont(gf);
    contentBody_->addWidget(greeting_);

    subtitle_ = new QLabel(contentPage_);
    QFont sf = subtitle_->font();
    sf.setPointSize(10);
    subtitle_->setFont(sf);
    subtitle_->setProperty("role", QStringLiteral("secondary"));
    contentBody_->addWidget(subtitle_);

    // A small toolbar at the top right of the content (rescan).
    auto* topBar = new QHBoxLayout;
    topBar->setContentsMargins(0, 0, 0, 0);
    topBar->addStretch(1);
    rescanBtn_ = new QPushButton(QStringLiteral("Rescan library"), contentPage_);
    rescanBtn_->setCursor(Qt::PointingHandCursor);
    connect(rescanBtn_, &QPushButton::clicked, this, &HomeView::onRescanClicked);
    topBar->addWidget(rescanBtn_);
    // Insert this row before the carousels (after the subtitle).
    contentBody_->insertLayout(2, topBar);

    contentBody_->addStretch(1);
    stack_->addWidget(contentPage_);

    // â”€â”€ Empty page â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    emptyPage_ = new QWidget(stack_);
    auto* ep = new QVBoxLayout(emptyPage_);
    ep->setContentsMargins(40, 40, 40, 40);
    ep->setSpacing(16);
    ep->setAlignment(Qt::AlignCenter);

    auto* bigIcon = new QLabel(QStringLiteral("\u2302"), emptyPage_);  // âŒ‚
    QFont bif = bigIcon->font();
    bif.setPointSize(72);
    bigIcon->setFont(bif);
    bigIcon->setAlignment(Qt::AlignCenter);
    bigIcon->setProperty("role", QStringLiteral("secondary"));
    ep->addWidget(bigIcon);

    auto* eTitle = new QLabel(QStringLiteral("Your library is empty"), emptyPage_);
    QFont etf = eTitle->font();
    etf.setPointSize(20);
    etf.setBold(true);
    eTitle->setFont(etf);
    eTitle->setAlignment(Qt::AlignCenter);
    ep->addWidget(eTitle);

    auto* eBody = new QLabel(
        QStringLiteral("Pick a folder of music and Musicefy will index it. "
                       "Favourites, recent plays, and playlists appear here once "
                       "tracks are in the library."),
        emptyPage_);
    eBody->setWordWrap(true);
    eBody->setAlignment(Qt::AlignCenter);
    eBody->setProperty("role", QStringLiteral("secondary"));
    ep->addWidget(eBody);

    auto* addBtn = new QPushButton(QStringLiteral("Add music folderâ€¦"), emptyPage_);
    addBtn->setCursor(Qt::PointingHandCursor);
    addBtn->setMinimumWidth(220);
    addBtn->setMinimumHeight(40);
    connect(addBtn, &QPushButton::clicked, this, &HomeView::onAddFolderClicked);
    ep->addWidget(addBtn, 0, Qt::AlignCenter);

    stack_->addWidget(emptyPage_);
    root->addWidget(stack_);
}

void HomeView::applyTheme() {
    MusicefyColorScheme s;
    if (theme_) s = theme_->scheme();
    if (!s.primary.isValid()) {
        setStyleSheet(QString());
        return;
    }
    setStyleSheet(QStringLiteral(
        "QWidget#HomeView { background: %1; color: %2; }"
        "QLabel { background: transparent; color: %2; }"
        "QLabel[role=\"secondary\"] { color: %3; }"
        "QPushButton { background: %4; color: %2;"
        "  border: 1px solid %5; border-radius: 6px;"
        "  padding: 6px 14px; }"
        "QPushButton:hover { background: %6; }"
    )
    .arg(s.background.name())
    .arg(s.onBackground.name())
    .arg(s.onSurfaceVariant.name())
    .arg(s.surfaceContainerHigh.name())
    .arg(s.outlineVariant.name())
    .arg(s.surfaceContainerHighest.name())
    );
}

void HomeView::onThemeChanged() {
    applyTheme();
    rebuildContent();
}

void HomeView::onContentChanged() {
    rebuildContent();
}

void HomeView::rebuildContent() {
    if (!vm_ || !contentBody_ || !stack_) return;

    // Update header.
    greeting_->setText(vm_->greeting());
    int tracks = vm_->recentlyPlayedCount() + vm_->favouritesCount();
    if (vm_->playlistsCount() > 0) {
        subtitle_->setText(QStringLiteral("%1 saved playlists Â· %2 tracks in your library")
                               .arg(vm_->playlistsCount()).arg(tracks));
    } else {
        subtitle_->setText(QStringLiteral("%1 tracks in your library").arg(tracks));
    }

    // Toggle between content and empty state.
    if (vm_->libraryIsEmpty()) {
        stack_->setCurrentWidget(emptyPage_);
        return;
    }
    stack_->setCurrentWidget(contentPage_);

    // Strip everything below the top toolbar (greeting + subtitle + topBar
    // are at indices 0, 1, 2) and re-add the carousels.
    // contentBody_->count() includes the trailing addStretch at the end.
    while (contentBody_->count() > 3) {
        QLayoutItem* item = contentBody_->takeAt(3);
        if (item) {
            clearLayout(item->layout());
            delete item->widget();
            delete item;
        }
    }

    // Quick access carousel.
    if (!vm_->quickAccess().isEmpty()) {
        QHBoxLayout* body = nullptr;
        auto* carousel = buildCarouselFrame(
            QStringLiteral("Quick access"),
            QStringLiteral("Play all"),
            &body);
        for (const auto& t : vm_->quickAccess()) {
            body->addWidget(buildTrackCard(t));
        }
        body->addStretch(1);
        contentBody_->addWidget(carousel);
    }

    // Recently played carousel.
    if (!vm_->recentlyPlayed().isEmpty()) {
        QHBoxLayout* body = nullptr;
        auto* carousel = buildCarouselFrame(
            QStringLiteral("Recently played"),
            QStringLiteral("Play all"),
            &body);
        for (const auto& t : vm_->recentlyPlayed()) {
            body->addWidget(buildTrackCard(t));
        }
        body->addStretch(1);
        contentBody_->addWidget(carousel);
    }

    // Favourites carousel.
    if (!vm_->favourites().isEmpty()) {
        QHBoxLayout* body = nullptr;
        auto* carousel = buildCarouselFrame(
            QStringLiteral("Favourites"),
            QStringLiteral("Play all"),
            &body);
        for (const auto& t : vm_->favourites()) {
            body->addWidget(buildTrackCard(t));
        }
        body->addStretch(1);
        contentBody_->addWidget(carousel);
    }

    // Most Played carousel.
    if (!vm_->mostPlayed().isEmpty()) {
        QHBoxLayout* body = nullptr;
        auto* carousel = buildCarouselFrame(
            QStringLiteral("Most played"),
            QStringLiteral("Play all"),
            &body);
        for (const auto& t : vm_->mostPlayed()) {
            body->addWidget(buildTrackCard(t));
        }
        body->addStretch(1);
        contentBody_->addWidget(carousel);
    }

    // Recently Added carousel.
    if (!vm_->recentlyAdded().isEmpty()) {
        QHBoxLayout* body = nullptr;
        auto* carousel = buildCarouselFrame(
            QStringLiteral("Recently added"),
            QStringLiteral("Play all"),
            &body);
        for (const auto& t : vm_->recentlyAdded()) {
            body->addWidget(buildTrackCard(t));
        }
        body->addStretch(1);
        contentBody_->addWidget(carousel);
    }

    // Playlists carousel.
    if (!vm_->playlists().isEmpty()) {
        QHBoxLayout* body = nullptr;
        auto* carousel = buildCarouselFrame(
            QStringLiteral("Your playlists"),
            QString(),
            &body);
        const auto& pls = vm_->playlists();
        for (int i = 0; i < pls.size(); ++i) {
            body->addWidget(buildPlaylistCard(pls[i], i));
        }
        body->addStretch(1);
        contentBody_->addWidget(carousel);
    }

    contentBody_->addStretch(1);
}

QFrame* HomeView::buildCarouselFrame(const QString& title,
                                     const QString& playAllLabel,
                                     QHBoxLayout**  bodyOut) {
    auto* frame = new QFrame(contentPage_);
    frame->setObjectName(QStringLiteral("CarouselFrame"));
    auto* layout = new QVBoxLayout(frame);
    layout->setContentsMargins(0, kCarouselPad, 0, kCarouselPad);
    layout->setSpacing(kCarouselGap);

    auto* titleBar = new QHBoxLayout;
    auto* titleLbl = new QLabel(title, frame);
    QFont tf = titleLbl->font();
    tf.setPointSize(14);
    tf.setBold(true);
    titleLbl->setFont(tf);
    titleBar->addWidget(titleLbl);
    titleBar->addStretch(1);

    if (!playAllLabel.isEmpty()) {
        auto* playAll = new QPushButton(playAllLabel, frame);
        playAll->setCursor(Qt::PointingHandCursor);
        playAll->setProperty("role", QStringLiteral("link"));
        if (title == QStringLiteral("Quick access")) {
            connect(playAll, &QPushButton::clicked,
                    this, &HomeView::onPlayAllQuick);
        } else if (title == QStringLiteral("Recently played")) {
            connect(playAll, &QPushButton::clicked,
                    this, &HomeView::onPlayAllRecent);
        } else if (title == QStringLiteral("Favourites")) {
            connect(playAll, &QPushButton::clicked,
                    this, &HomeView::onPlayAllFav);
        } else if (title == QStringLiteral("Most played")) {
            connect(playAll, &QPushButton::clicked,
                    this, &HomeView::onPlayAllMostPlayed);
        } else if (title == QStringLiteral("Recently added")) {
            connect(playAll, &QPushButton::clicked,
                    this, &HomeView::onPlayAllRecentlyAdded);
        }
        titleBar->addWidget(playAll);
    }
    layout->addLayout(titleBar);

    auto* scroll = new QScrollArea(frame);
    scroll->setWidgetResizable(true);
    scroll->setHorizontalScrollBarPolicy(Qt::ScrollBarAsNeeded);
    scroll->setVerticalScrollBarPolicy(Qt::ScrollBarAlwaysOff);
    scroll->setFrameShape(QFrame::NoFrame);
    scroll->setFixedHeight(kCardHeight + 24);

    auto* body = new QWidget(scroll);
    body->setObjectName(QStringLiteral("CarouselBody"));
    auto* hbox = new QHBoxLayout(body);
    hbox->setContentsMargins(0, 0, 0, 0);
    hbox->setSpacing(kCarouselGap);
    scroll->setWidget(body);
    layout->addWidget(scroll);

    *bodyOut = hbox;
    return frame;
}

QFrame* HomeView::buildTrackCard(const MusicFile& track) {
    auto* card = new Card(contentPage_);
    card->setObjectName(QStringLiteral("TrackCard"));
    card->setTitle(track.title().isEmpty() ? QFileInfo(track.filePath()).fileName() : track.title());
    card->setSubtitle(track.artist());

    auto* lay = new QVBoxLayout(card);
    lay->setContentsMargins(6, 6, 6, 6);
    lay->setSpacing(6);

    MusicefyColorScheme s;
    if (theme_) s = theme_->scheme();
    QColor cover = s.primary.isValid() ? s.primaryContainer : QColor(40, 40, 40);
    card->setCoverColor(cover);

    // Real cover art. The CoverImage widget handles the async
    // (ImageCache) and sync (PathToImage) paths, and falls back to
    // a letter placeholder with a stable hue if no path is set.
    QString initial = card->title().isEmpty()
        ? QStringLiteral("?")
        : QString(card->title().at(0)).toUpper();
    auto* coverImg = new CoverImage(imageCache_, card);
    coverImg->setSource(track.coverPath(), initial);
    coverImg->setFixedSize(kCoverSize, kCoverSize);
    lay->addWidget(coverImg);

    auto* titleLbl = new QLabel(card->title(), card);
    titleLbl->setWordWrap(true);
    titleLbl->setMaximumHeight(34);
    QFont tlf = titleLbl->font();
    tlf.setPointSize(10);
    tlf.setBold(true);
    titleLbl->setFont(tlf);
    lay->addWidget(titleLbl);
    card->titleLabel_ = titleLbl;

    auto* subLbl = new QLabel(card->subtitle(), card);
    subLbl->setWordWrap(true);
    QFont slf = subLbl->font();
    slf.setPointSize(9);
    subLbl->setFont(slf);
    subLbl->setProperty("role", QStringLiteral("secondary"));
    subLbl->setStyleSheet(QStringLiteral(
        "QLabel[role=\"secondary\"] { color: %1; }"
    ).arg(s.onSurfaceVariant.name()));
    lay->addWidget(subLbl);
    card->subLabel_ = subLbl;

    QString fp = track.filePath();
    connect(card, &Card::activated, this, [this, fp]() {
        onTrackActivated(fp);
    });
    // Install hover glow effect
    CardColorGlowBehavior::install(card, imageCache_);
    return card;
}

QFrame* HomeView::buildPlaylistCard(const PlaylistInfo& pl, int index) {
    auto* card = new Card(contentPage_);
    card->setObjectName(QStringLiteral("PlaylistCard"));
    card->setTitle(pl.name());
    QString sub = QStringLiteral("%1 tracks").arg(pl.trackCount());
    if (pl.totalDuration().count() > 0) {
        qint64 s = pl.totalDuration().count();
        sub += QStringLiteral(" Â· %1:%2")
            .arg(s / 60)
            .arg(s % 60, 2, 10, QLatin1Char('0'));
    }
    card->setSubtitle(sub);

    auto* lay = new QVBoxLayout(card);
    lay->setContentsMargins(6, 6, 6, 6);
    lay->setSpacing(6);

    MusicefyColorScheme s;
    if (theme_) s = theme_->scheme();
    QColor cover = s.tertiary.isValid() ? s.tertiaryContainer : QColor(60, 60, 60);
    card->setCoverColor(cover);

    // Real cover art (or â™ª placeholder with a stable hue when
    // the playlist has no coverPath set).
    auto* coverImg = new CoverImage(imageCache_, card);
    coverImg->setSource(pl.coverPath(), QStringLiteral("\u266B"));
    coverImg->setFixedSize(kCoverSize, kCoverSize);
    lay->addWidget(coverImg);

    auto* titleLbl = new QLabel(card->title(), card);
    titleLbl->setWordWrap(true);
    titleLbl->setMaximumHeight(34);
    QFont tlf = titleLbl->font();
    tlf.setPointSize(10);
    tlf.setBold(true);
    titleLbl->setFont(tlf);
    lay->addWidget(titleLbl);
    card->titleLabel_ = titleLbl;

    auto* subLbl = new QLabel(card->subtitle(), card);
    QFont slf = subLbl->font();
    slf.setPointSize(9);
    subLbl->setFont(slf);
    subLbl->setProperty("role", QStringLiteral("secondary"));
    subLbl->setStyleSheet(QStringLiteral(
        "QLabel[role=\"secondary\"] { color: %1; }"
    ).arg(s.onSurfaceVariant.name()));
    lay->addWidget(subLbl);
    card->subLabel_ = subLbl;

    connect(card, &Card::activated, this, [this, index]() {
        onPlaylistActivated(index);
    });
    // Install hover glow effect
    CardColorGlowBehavior::install(card, imageCache_);
    return card;
}

void HomeView::clearLayout(QLayout* layout) {
    if (!layout) return;
    while (auto* item = layout->takeAt(0)) {
        if (item->widget()) {
            item->widget()->deleteLater();
        }
        if (item->layout()) {
            clearLayout(item->layout());
        }
        delete item;
    }
}

void HomeView::onAddFolderClicked() {
    if (!libSvc_) return;
    const QString start = libSvc_->folders().isEmpty()
        ? QDir::homePath()
        : libSvc_->folders().first();
    QString dir = QFileDialog::getExistingDirectory(
        this, QStringLiteral("Add music folder"), start);
    if (dir.isEmpty()) return;
    if (!libSvc_->addFolder(dir)) {
        // The LibraryService will have already triggered a scan if
        // addFolder succeeded; nothing more to do. On failure we
        // don't surface a toast here (the settings panel does that).
    }
}

void HomeView::onRescanClicked() {
    if (!libSvc_) return;
    if (libSvc_->isScanning()) return;
    if (libSvc_->folders().isEmpty()) {
        onAddFolderClicked();
        return;
    }
    libSvc_->rescan();
}

void HomeView::onPlayAllQuick() {
    if (vm_) vm_->playAllFromQuickAccess();
}

void HomeView::onPlayAllRecent() {
    if (vm_) vm_->playAllFromRecentlyPlayed();
}

void HomeView::onPlayAllFav() {
    if (vm_) vm_->playAllFromFavourites();
}

void HomeView::onPlayAllMostPlayed() {
    if (vm_) vm_->playAllFromMostPlayed();
}

void HomeView::onPlayAllRecentlyAdded() {
    if (vm_) vm_->playAllFromRecentlyAdded();
}

void HomeView::onTrackActivated(const QString& filePath) {
    if (vm_) vm_->playTrack(filePath);
}

void HomeView::onPlaylistActivated(int index) {
    if (vm_) vm_->openPlaylistAt(index);
}

} // namespace mf::app::widgets

#include "HomeView.moc"
