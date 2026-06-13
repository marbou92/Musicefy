// PlayerViewModel.cpp
// See header. All forwarding — no caching of value types that the
// services can change asynchronously (we always re-read).

#include "PlayerViewModel.h"

#include "../../core/database/LibraryRepository.h"
#include "../../core/models/MusicFileExtensions.h"
#include "../../core/services/LyricsService.h"
#include "../../core/services/ScrobbleService.h"
#include "../../core/services/ReplayGainService.h"
#include "../../core/services/EqualizerService.h"

#include <QClipboard>
#include <QCoreApplication>
#include <QGuiApplication>
#include <QDesktopServices>
#include <QDir>
#include <QFileInfo>
#include <QProcess>
#include <QUrl>
#include <algorithm>

namespace mf::app::viewmodels {

using mf::core::playback::PlaybackService;
using mf::core::playback::QueueManager;
using mf::core::models::MusicFile;

PlayerViewModel::PlayerViewModel(PlaybackService* playback,
                                 QueueManager*    queue,
                                 mf::core::database::LibraryRepository* repo,
                                 QObject*         parent)
    : QObject(parent)
    , playback_(playback)
    , queue_(queue)
    , repo_(repo)
{
    // Hook up to the service signals and re-emit the matching
    // Q_PROPERTY NOTIFY. We also update our internal state cache so
    // a coalesced set of changes can be observed atomically.
    if (playback_) {
        connect(playback_, &PlaybackService::stateChangedQ,
                this, [this](int) { emitAll(); });
        connect(playback_, &PlaybackService::trackChangedQ,
                this, [this]() {
            emit currentTrackChanged();
            updateFavoriteState();
            updateAudioFormatText();
            updateLyrics();
            notifyScrobble();
            applyAudioEffects();
        });
        connect(playback_, &PlaybackService::positionChangedQ,
                this, [this](qint64) { emit positionChanged(); });
        connect(playback_, &PlaybackService::durationChangedQ,
                this, [this](qint64) { emit durationChanged(); });
        connect(playback_, &PlaybackService::volumeChangedQ,
                this, [this](float) { emit volumeChanged(); });
        connect(playback_, &PlaybackService::mutedChangedQ,
                this, [this](bool)  { emit mutedChanged(); });
        connect(playback_, &PlaybackService::errorOccurredQ,
                this, &PlayerViewModel::errorReported);
    }
    if (queue_) {
        connect(queue_, &QueueManager::indexChangedQ,
                this, [this](int) {
            emit currentTrackChanged();
            emit navigationChanged();
            updateFavoriteState();
            updateAudioFormatText();
            updateLyrics();
        });
        connect(queue_, &QueueManager::queueChangedQ,
                this, [this]() {
            emit queueChanged();
            emit navigationChanged();
        });
        connect(queue_, &QueueManager::shuffleChangedQ,
                this, &PlayerViewModel::shuffleChanged);
        connect(queue_, &QueueManager::repeatChangedQ,
                this, &PlayerViewModel::repeatChanged);
        // Persist queue on every mutation.
        QObject::connect(queue_, &QueueManager::queueChangedQ,
                         this, [queue]() { queue->persist(); });
        QObject::connect(queue_, &QueueManager::indexChangedQ,
                         this, [queue](int) { queue->persist(); });
    }
    emitAll();
}

void PlayerViewModel::emitAll() {
    bool playing = isPlaying();
    bool paused  = isPaused();
    bool stopped = isStopped();
    if (playing != isPlayingCache_) {
        isPlayingCache_ = playing;
        emit isPlayingChanged();
    }
    if (paused != isPausedCache_) {
        isPausedCache_ = paused;
        emit isPausedChanged();
    }
    if (stopped != isStoppedCache_) {
        isStoppedCache_ = stopped;
        emit isStoppedChanged();
    }
    emit positionChanged();
    emit durationChanged();
}

// ── Read accessors ───────────────────────────────────────────────────

bool PlayerViewModel::isPlaying() const {
    if (!playback_) return false;
    return playback_->state() == PlaybackService::PlaybackState::Playing;
}
bool PlayerViewModel::isPaused() const {
    if (!playback_) return false;
    return playback_->state() == PlaybackService::PlaybackState::Paused;
}
bool PlayerViewModel::isStopped() const {
    if (!playback_) return true;
    return playback_->state() == PlaybackService::PlaybackState::Stopped;
}
qint64 PlayerViewModel::positionMs() const {
    return playback_ ? static_cast<qint64>(playback_->position().count()) : 0;
}
qint64 PlayerViewModel::durationMs() const {
    return playback_ ? static_cast<qint64>(playback_->duration().count()) : 0;
}
double PlayerViewModel::positionPercent() const {
    qint64 dur = durationMs();
    if (dur <= 0) return 0.0;
    return std::clamp(double(positionMs()) / double(dur), 0.0, 1.0);
}
float PlayerViewModel::volume() const {
    return playback_ ? playback_->volume() : 0.0f;
}
bool PlayerViewModel::isMuted() const {
    return playback_ ? playback_->isMuted() : false;
}
bool PlayerViewModel::hasCurrentTrack() const {
    if (!queue_) return false;
    auto t = queue_->currentTrack();
    return !t.filePath().isEmpty() || !t.sourceUri().isEmpty();
}
QString PlayerViewModel::currentTitle()  const { return currentTrack().title(); }
QString PlayerViewModel::currentArtist() const { return currentTrack().artist(); }
QString PlayerViewModel::currentAlbum()  const { return currentTrack().album(); }
QString PlayerViewModel::currentSourceUri() const { return currentTrack().sourceUri(); }
bool PlayerViewModel::hasNext()     const { return queue_ ? queue_->hasNext()     : false; }
bool PlayerViewModel::hasPrevious() const { return queue_ ? queue_->hasPrevious() : false; }
bool PlayerViewModel::shuffle()     const { return queue_ ? queue_->isShuffle()   : false; }
int  PlayerViewModel::repeatMode()  const { return queue_ ? int(queue_->repeatMode()) : 0; }
int  PlayerViewModel::queueCount()  const { return queue_ ? queue_->count()      : 0; }
MusicFile PlayerViewModel::currentTrack() const {
    if (!queue_) return MusicFile();
    return queue_->currentTrack();
}

// ── Commands ─────────────────────────────────────────────────────────

void PlayerViewModel::play()   { if (playback_) playback_->play(); }
void PlayerViewModel::pause()  { if (playback_) playback_->pause(); }
void PlayerViewModel::togglePlayPause() { if (playback_) playback_->togglePlayPause(); }
void PlayerViewModel::stop()   { if (playback_) playback_->stop(); }
void PlayerViewModel::next()   { if (queue_)    queue_->next(); }
void PlayerViewModel::previous() { if (queue_)  queue_->previous(); }
void PlayerViewModel::jumpTo(int index) { if (queue_) queue_->setCurrentIndex(index); }
void PlayerViewModel::seekMs(qint64 ms) { if (playback_) playback_->seek(std::chrono::milliseconds(ms)); }
void PlayerViewModel::seekPercent(double percent) {
    if (!playback_) return;
    qint64 dur = durationMs();
    if (dur <= 0) return;
    qint64 target = static_cast<qint64>(std::clamp(percent, 0.0, 1.0) * double(dur));
    seekMs(target);
}
void PlayerViewModel::setVolume(float v) { if (playback_) playback_->setVolume(v); }
void PlayerViewModel::setMuted(bool m)   { if (playback_) playback_->setMuted(m); }
void PlayerViewModel::setShuffle(bool enabled) { if (queue_) queue_->setShuffle(enabled); }
void PlayerViewModel::cycleRepeat() {
    if (!queue_) return;
    using RM = QueueManager::RepeatMode;
    RM cur = queue_->repeatMode();
    RM next = (cur == RM::Off) ? RM::All
            : (cur == RM::All) ? RM::One
            : RM::Off;
    queue_->setRepeatMode(next);
}

// ── Favorite / Share / Explorer ────────────────────────────────────

void PlayerViewModel::updateFavoriteState()
{
    const auto track = currentTrack();
    const bool fav = track.isFavourite();
    if (isFavorite_ != fav) {
        isFavorite_ = fav;
        emit isFavoriteChanged();
    }
}

void PlayerViewModel::updateAudioFormatText()
{
    const auto track = currentTrack();
    if (track.filePath().isEmpty() && track.sourceUri().isEmpty()) {
        audioFormatText_.clear();
        emit audioFormatChanged();
        return;
    }

    QString format = track.sourceType().toUpper();
    if (format.isEmpty()) format = QStringLiteral("AUDIO");

    if (track.bitrate() > 0) {
        const double sizeMB = track.fileSize() > 0
            ? double(track.fileSize()) / (1024.0 * 1024.0) : 0.0;
        audioFormatText_ = QStringLiteral("%1 \u2022 %2 kbps \u2023 %3 MB")
            .arg(format)
            .arg(track.bitrate())
            .arg(sizeMB, 0, 'f', 1);
    } else {
        audioFormatText_ = format;
    }
    emit audioFormatChanged();
}

void PlayerViewModel::updateLyrics()
{
    const auto track = currentTrack();

    // Try embedded lyrics first.
    const QString embedded = track.lyrics();
    if (!embedded.isEmpty()) {
        if (currentLyrics_ != embedded) {
            currentLyrics_ = embedded;
            emit currentTrackChanged(); // includes currentLyrics
        }
        return;
    }

    // Fall back to external lyrics fetch via LyricsService.
    if (lyricsSvc_) {
        lyricsSvc_->fetchLyrics(track, [this](const QString& fetched) {
            if (!fetched.isEmpty() && currentLyrics_ != fetched) {
                currentLyrics_ = fetched;
                emit currentTrackChanged();
            }
        });
    } else if (!currentLyrics_.isEmpty()) {
        currentLyrics_.clear();
        emit currentTrackChanged();
    }
}

void PlayerViewModel::notifyScrobble()
{
    if (!scrobbler_) return;
    const auto track = currentTrack();
    if (track.title().isEmpty()) return;
    scrobbler_->nowPlaying(track);
    // Track finished scrobbling will be called when the track changes
    // or playback stops — for simplicity we scrobble on track change.
    scrobbler_->trackFinished(track, static_cast<int>(positionMs() / 1000));

    // Increment play count in library.
    if (repo_ && !track.filePath().isEmpty()) {
        repo_->incrementPlayCount(track.filePath());
    }
}

void PlayerViewModel::toggleFavorite()
{
    const auto track = currentTrack();
    if (track.filePath().isEmpty()) return;

    // Optimistic update
    isFavorite_ = !isFavorite_;
    emit isFavoriteChanged();

    // Persist via repo
    if (repo_) {
        repo_->toggleFavourite(track.filePath());
    }
}

void PlayerViewModel::shareCurrentTrack()
{
    const auto track = currentTrack();
    if (track.title().isEmpty()) return;

    const QString text = QStringLiteral("%1 - %2").arg(track.title(), track.artist());
    if (auto* clipboard = QGuiApplication::clipboard())
        clipboard->setText(text);
}

void PlayerViewModel::showInExplorer()
{
    const auto track = currentTrack();
    const QString path = track.filePath();
    if (path.isEmpty()) return;

    const QFileInfo fi(path);
    if (!fi.exists()) return;

    // Skip UNC paths
    if (path.startsWith(QLatin1String("\\\\"))) return;

    QProcess::startDetached(QStringLiteral("explorer.exe"),
                            {QStringLiteral("/select,\"%1\"").arg(fi.absoluteFilePath())});
}

void PlayerViewModel::playTrackWithDirectory(const MusicFile& track) {
    if (!queue_ || !playback_) return;

    queue_->clear();

    // Enqueue the selected track first.
    queue_->enqueue(track);

    // If it's a local file, find and enqueue sibling audio files.
    const QString path = track.filePath();
    if (!path.isEmpty() && !track.sourceUri().startsWith("http")) {
        QFileInfo fi(path);
        QDir dir = fi.absoluteDir();
        if (dir.exists()) {
            const QStringList& suffixList = mf::core::models::MusicFileExtensions::SuffixList();
            QStringList filters;
            for (const auto& ext : suffixList) {
                filters << QStringLiteral("*.%1").arg(ext);
            }
            QFileInfoList files = dir.entryInfoList(filters, QDir::Files, QDir::Name);
            for (const auto& sibling : files) {
                if (sibling.absoluteFilePath() == fi.absoluteFilePath())
                    continue;
                MusicFile siblingTrack;
                siblingTrack.setFilePath(sibling.absoluteFilePath());
                siblingTrack.setTitle(sibling.completeBaseName());
                siblingTrack.setArtist(track.artist());
                siblingTrack.setAlbum(track.album());
                siblingTrack.setSourceType(QStringLiteral("Local"));
                queue_->enqueue(std::move(siblingTrack));
            }
        }
    }

    // Start playback of the first track.
    playback_->setTrack(queue_->currentTrack());
    playback_->play();
}

void PlayerViewModel::applyAudioEffects()
{
    // Compute effective volume: user volume * ReplayGain * EQ preamp.
    const float effective = computeEffectiveVolume();
    if (playback_) {
        playback_->setVolume(effective);
    }
}

float PlayerViewModel::computeEffectiveVolume() const
{
    float base = playback_ ? playback_->volume() : 0.5f;

    // ReplayGain adjustment.
    float rgMult = 1.0f;
    if (replayGain_) {
        const auto track = currentTrack();
        if (!track.filePath().isEmpty()) {
            rgMult = replayGain_->volumeMultiplier(track.filePath());
        }
    }

    // Equalizer preamp adjustment.
    float eqMult = 1.0f;
    if (equalizer_ && equalizer_->isEnabled()) {
        eqMult = equalizer_->preampMultiplier();
    }

    float effective = base * rgMult * eqMult;
    // Clamp to [0.0, 1.0] for QMediaPlayer Qt5 range.
    effective = std::clamp(effective, 0.0f, 1.0f);
    return effective;
}

} // namespace mf::app::viewmodels