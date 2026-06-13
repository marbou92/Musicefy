// PlayerViewModel.h
// Bindable, QML-friendly view of the playback stack. Reads from
// PlaybackService and QueueManager, republishes state as Q_PROPERTYs,
// and forwards commands back down. No business logic lives here —
// all timing/queue decisions stay in the services.

#pragma once

#include "../../core/playback/PlaybackService.h"
#include "../../core/playback/QueueManager.h"
#include "../../core/models/MusicFile.h"

#include <QObject>
#include <memory>

namespace mf::core::playback   { class PlaybackService; class QueueManager; }
namespace mf::core::database   { class LibraryRepository; }
namespace mf::core::services   { class LyricsService; class ScrobbleService;
                                 class ReplayGainService; class EqualizerService; }

namespace mf::app::viewmodels {

class PlayerViewModel : public QObject {
    Q_OBJECT
    Q_PROPERTY(bool      isPlaying         READ isPlaying        NOTIFY isPlayingChanged)
    Q_PROPERTY(bool      isPaused          READ isPaused         NOTIFY isPausedChanged)
    Q_PROPERTY(bool      isStopped         READ isStopped        NOTIFY isStoppedChanged)
    Q_PROPERTY(qint64    positionMs        READ positionMs       NOTIFY positionChanged)
    Q_PROPERTY(qint64    durationMs        READ durationMs       NOTIFY durationChanged)
    Q_PROPERTY(double    positionPercent   READ positionPercent  NOTIFY positionChanged)
    Q_PROPERTY(float     volume            READ volume           WRITE setVolume NOTIFY volumeChanged)
    Q_PROPERTY(bool      muted             READ isMuted          WRITE setMuted  NOTIFY mutedChanged)
    Q_PROPERTY(bool      hasCurrentTrack   READ hasCurrentTrack  NOTIFY currentTrackChanged)
    Q_PROPERTY(QString   currentTitle      READ currentTitle     NOTIFY currentTrackChanged)
    Q_PROPERTY(QString   currentArtist     READ currentArtist    NOTIFY currentTrackChanged)
    Q_PROPERTY(QString   currentAlbum      READ currentAlbum     NOTIFY currentTrackChanged)
    Q_PROPERTY(QString   currentSourceUri  READ currentSourceUri NOTIFY currentTrackChanged)
    Q_PROPERTY(bool      hasNext           READ hasNext          NOTIFY navigationChanged)
    Q_PROPERTY(bool      hasPrevious       READ hasPrevious      NOTIFY navigationChanged)
    Q_PROPERTY(bool      shuffle           READ shuffle          WRITE setShuffle NOTIFY shuffleChanged)
    Q_PROPERTY(int       repeatMode        READ repeatMode       NOTIFY repeatChanged)
    Q_PROPERTY(int       queueCount        READ queueCount       NOTIFY queueChanged)
    Q_PROPERTY(bool      isFavorite        READ isFavorite       NOTIFY isFavoriteChanged)
    Q_PROPERTY(QString   audioFormatText   READ audioFormatText  NOTIFY audioFormatChanged)
    Q_PROPERTY(QString   currentLyrics     READ currentLyrics    NOTIFY currentTrackChanged)

public:
    PlayerViewModel(mf::core::playback::PlaybackService* playback,
                    mf::core::playback::QueueManager*    queue,
                    mf::core::database::LibraryRepository* repo,
                    QObject* parent = nullptr);
    ~PlayerViewModel() override = default;

    // ── External service injection ──────────────────────────────────────
    void setLyricsService(mf::core::services::LyricsService* svc) { lyricsSvc_ = svc; }
    void setScrobbler(mf::core::services::ScrobbleService* svc) { scrobbler_ = svc; }
    void setReplayGain(mf::core::services::ReplayGainService* svc) { replayGain_ = svc; }
    void setEqualizer(mf::core::services::EqualizerService* svc) { equalizer_ = svc; }

    // ── Read accessors (exposed to QML/Widgets) ────────────────────────
    bool    isPlaying()        const;
    bool    isPaused()         const;
    bool    isStopped()        const;
    qint64  positionMs()       const;
    qint64  durationMs()       const;
    double  positionPercent()  const;
    float   volume()           const;
    bool    isMuted()          const;
    bool    hasCurrentTrack()  const;
    QString currentTitle()     const;
    QString currentArtist()    const;
    QString currentAlbum()     const;
    QString currentSourceUri() const;
    bool    hasNext()          const;
    bool    hasPrevious()      const;
    bool    shuffle()          const;
    int     repeatMode()       const;
    int     queueCount()       const;
    bool    isFavorite()       const { return isFavorite_; }
    QString audioFormatText()  const { return audioFormatText_; }
    QString currentLyrics()    const { return currentLyrics_; }

    // ── Commands (Q_INVOKABLE for QML, public for Widgets) ─────────────
    Q_INVOKABLE void play();
    Q_INVOKABLE void pause();
    Q_INVOKABLE void togglePlayPause();
    Q_INVOKABLE void stop();
    Q_INVOKABLE void next();
    Q_INVOKABLE void previous();
    Q_INVOKABLE void jumpTo(int index);
    Q_INVOKABLE void seekMs(qint64 ms);
    Q_INVOKABLE void seekPercent(double percent);
    Q_INVOKABLE void setVolume(float v);
    Q_INVOKABLE void setMuted(bool m);
    Q_INVOKABLE void setShuffle(bool enabled);
    Q_INVOKABLE void cycleRepeat();
    Q_INVOKABLE void toggleFavorite();
    Q_INVOKABLE void shareCurrentTrack();
    Q_INVOKABLE void showInExplorer();
    Q_INVOKABLE void playTrackWithDirectory(const mf::core::models::MusicFile& track);

    mf::core::models::MusicFile currentTrack() const;

signals:
    void isPlayingChanged();
    void isPausedChanged();
    void isStoppedChanged();
    void positionChanged();
    void durationChanged();
    void volumeChanged();
    void mutedChanged();
    void currentTrackChanged();
    void navigationChanged();
    void shuffleChanged();
    void repeatChanged();
    void queueChanged();
    void isFavoriteChanged();
    void audioFormatChanged();
    void currentLyricsChanged();
    void errorReported(const QString& message);

private:
    void emitAll();
    void updateFavoriteState();
    void updateAudioFormatText();
    void updateLyrics();
    void notifyScrobble();
    void applyAudioEffects();
    float computeEffectiveVolume() const;

    mf::core::playback::PlaybackService* playback_ = nullptr;
    mf::core::playback::QueueManager*    queue_    = nullptr;
    mf::core::database::LibraryRepository* repo_   = nullptr;
    mf::core::services::LyricsService*   lyricsSvc_ = nullptr;
    mf::core::services::ScrobbleService* scrobbler_ = nullptr;
    mf::core::services::ReplayGainService* replayGain_ = nullptr;
    mf::core::services::EqualizerService*  equalizer_  = nullptr;

    bool isPlayingCache_ = false;
    bool isPausedCache_  = false;
    bool isStoppedCache_ = true;

    bool    isFavorite_ = false;
    QString audioFormatText_;
    QString currentLyrics_;
    int     trackPlayedSec_ = 0;
};

} // namespace mf::app::viewmodels
