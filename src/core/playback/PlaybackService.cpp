// PlaybackService.cpp
// QMediaPlayer + QAudioOutput wrapper. Translates Qt's PlaybackState to
// our IAudioPlayer::PlaybackState and routes signals to callback slots.

#include "PlaybackService.h"

#include <QAudioOutput>
#include <QMediaContent>
#include <QMediaPlayer>
#include <QUrl>
#include <QDebug>

namespace mf::core::playback {

using mf::core::interfaces::IAudioPlayer;
using mf::core::models::MusicFile;

namespace {

IAudioPlayer::PlaybackState fromQt(QMediaPlayer::State s) {
    switch (s) {
        case QMediaPlayer::StoppedState:  return IAudioPlayer::PlaybackState::Stopped;
        case QMediaPlayer::PlayingState:  return IAudioPlayer::PlaybackState::Playing;
        case QMediaPlayer::PausedState:   return IAudioPlayer::PlaybackState::Paused;
    }
    return IAudioPlayer::PlaybackState::Stopped;
}

QString sourceUriFor(const MusicFile& m) {
    // Local files play from filePath. Streams play from sourceUri.
    QString uri = m.sourceUri();
    if (uri.isEmpty()) {
        uri = m.filePath();
    }
    if (uri.isEmpty()) {
        return QString();
    }
    // If it already looks like a URL, pass through; otherwise wrap as a local file URL.
    if (uri.contains(QStringLiteral("://"))) {
        return uri;
    }
    return QUrl::fromLocalFile(uri).toString();
}

} // namespace

PlaybackService::PlaybackService(QObject* parent)
    : QObject(parent)
    , audioOutput_(std::make_unique<QAudioOutput>())
    , player_(std::make_unique<QMediaPlayer>())
{
    connect(player_.get(), &QMediaPlayer::stateChanged,
            this, &PlaybackService::onMediaStateChanged);
    connect(player_.get(), &QMediaPlayer::mediaStatusChanged,
            this, &PlaybackService::onMediaStatusChanged);
    connect(player_.get(), &QMediaPlayer::positionChanged,
            this, &PlaybackService::onPositionChanged);
    connect(player_.get(), &QMediaPlayer::durationChanged,
            this, &PlaybackService::onDurationChanged);
    connect(player_.get(), qOverload<QMediaPlayer::Error>(&QMediaPlayer::error),
            this, &PlaybackService::onErrorOccurred);
    // Qt5: volume/mute signals are on QMediaPlayer
    connect(player_.get(), qOverload<int>(&QMediaPlayer::volumeChanged), this, [this](int v) {
        emit volumeChangedQ(v / 100.0f);
    });
    connect(player_.get(), &QMediaPlayer::mutedChanged, this, [this](bool m) {
        emit mutedChangedQ(m);
    });
}

PlaybackService::~PlaybackService() = default;

void PlaybackService::play() {
    if (player_->mediaStatus() == QMediaPlayer::NoMedia) {
        return;
    }
    player_->play();
}

void PlaybackService::pause() {
    player_->pause();
}

void PlaybackService::stop() {
    player_->stop();
}

void PlaybackService::togglePlayPause() {
    switch (player_->state()) {
        case QMediaPlayer::PlayingState: pause();  break;
        case QMediaPlayer::PausedState:
        case QMediaPlayer::StoppedState: play();   break;
    }
}

void PlaybackService::seek(std::chrono::milliseconds position) {
    player_->setPosition(qint64(position.count()));
}

void PlaybackService::setVolume(float volume) {
    int percent = static_cast<int>(std::clamp(volume, 0.0f, 1.0f) * 100.0f);
    player_->setVolume(percent);
}

float PlaybackService::volume() const {
    return player_->volume() / 100.0f;
}

void PlaybackService::setMuted(bool muted) {
    player_->setMuted(muted);
}

bool PlaybackService::isMuted() const {
    return player_->isMuted();
}

void PlaybackService::setTrack(MusicFile track) {
    currentTrack_ = std::move(track);

    QString uri = sourceUriFor(currentTrack_);
    if (uri.isEmpty()) {
        player_->setMedia(QMediaContent());
    } else {
        player_->setMedia(QMediaContent(QUrl(uri)));
    }

    if (onTrackChanged_) {
        onTrackChanged_(currentTrack_);
    }
    emit trackChangedQ();
}

std::chrono::milliseconds PlaybackService::position() const {
    return std::chrono::milliseconds(player_->position());
}

std::chrono::milliseconds PlaybackService::duration() const {
    return std::chrono::milliseconds(player_->duration());
}

IAudioPlayer::PlaybackState PlaybackService::state() const {
    return fromQt(player_->state());
}

void PlaybackService::onMediaStateChanged(QMediaPlayer::State s) {
    Q_UNUSED(s);
    emitState();
}

void PlaybackService::onMediaStatusChanged(QMediaPlayer::MediaStatus status) {
    // Buffering shows up here. The UI uses it to display a spinner.
    if (status == QMediaPlayer::BufferingMedia) {
        if (onStateChanged_) {
            onStateChanged_(int(IAudioPlayer::PlaybackState::Buffering));
        }
        emit stateChangedQ(int(IAudioPlayer::PlaybackState::Buffering));
    } else if (status == QMediaPlayer::InvalidMedia) {
        if (onError_) {
            onError_(QStringLiteral("Invalid media"));
        }
        emit errorOccurredQ(QStringLiteral("Invalid media"));
    } else {
        // Re-emit the actual playback state so listeners resync.
        emitState();
    }
}

void PlaybackService::onPositionChanged(qint64 pos) {
    if (onPositionChanged_) {
        onPositionChanged_(std::chrono::milliseconds(pos));
    }
    emit positionChangedQ(pos);
}

void PlaybackService::onDurationChanged(qint64 dur) {
    Q_UNUSED(dur);
    emit durationChangedQ(dur);
}

void PlaybackService::onErrorOccurred(int err) {
    QString message;
    switch (static_cast<QMediaPlayer::Error>(err)) {
        case QMediaPlayer::ResourceError:      message = QStringLiteral("Resource error"); break;
        case QMediaPlayer::FormatError:       message = QStringLiteral("Format error"); break;
        case QMediaPlayer::NetworkError:      message = QStringLiteral("Network error"); break;
        case QMediaPlayer::AccessDeniedError: message = QStringLiteral("Access denied"); break;
        case QMediaPlayer::NoError:           return;
    }
    if (player_->errorString().isEmpty() == false) {
        message = player_->errorString();
    }
    if (onError_) {
        onError_(message);
    }
    if (onStateChanged_) {
        onStateChanged_(int(IAudioPlayer::PlaybackState::Error));
    }
    emit errorOccurredQ(message);
    emit stateChangedQ(int(IAudioPlayer::PlaybackState::Error));
}

void PlaybackService::emitState() {
    PlaybackState s = fromQt(player_->state());
    if (s == lastEmittedState_) {
        return;
    }
    lastEmittedState_ = s;
    if (onStateChanged_) {
        onStateChanged_(int(s));
    }
    emit stateChangedQ(int(s));
}

} // namespace mf::core::playback