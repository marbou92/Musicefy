// ScrobbleService.h
// Submits play data to Last.fm scrobble API.
// Scrobbles are queued in memory and flushed on a short timer.
// Auth credentials are read from SettingsControl.

#pragma once

#include <QObject>
#include <QString>
#include <QQueue>
#include <QTimer>

namespace mf::core::models { class MusicFile; }
namespace mf::core::sources { class HttpClient; }
namespace mf::core::services { class SettingsControl; }

namespace mf::core::services {

/// Minimum seconds a track must be played before it qualifies for scrobbling.
/// Last.fm spec: 50% of track duration or 240 seconds, whichever comes first.
constexpr int kScrobbleMinSec = 240;

struct ScrobbleRecord {
    QString artist;
    QString track;
    QString album;
    int     durationSec = 0;
    qint64  timestamp   = 0;  // Unix epoch seconds
};

class ScrobbleService : public QObject {
    Q_OBJECT
    Q_PROPERTY(bool enabled READ isEnabled WRITE setEnabled NOTIFY enabledChanged)
    Q_PROPERTY(bool authenticated READ isAuthenticated NOTIFY authenticatedChanged)
public:
    explicit ScrobbleService(mf::core::sources::HttpClient* http,
                             mf::core::services::SettingsControl* settings,
                             QObject* parent = nullptr);
    ~ScrobbleService() override;

    bool isEnabled() const { return enabled_; }
    void setEnabled(bool v);

    bool isAuthenticated() const;

    /// Call when a track starts playing. Updates "now playing" on Last.fm.
    void nowPlaying(const mf::core::models::MusicFile& track);

    /// Call when a track finishes or the user seeks past the scrobble threshold.
    /// The service decides whether to actually submit based on play duration.
    void trackFinished(const mf::core::models::MusicFile& track,
                       int playedDurationSec);

    /// Manually flush the scrobble queue (e.g. on app shutdown).
    void flush();

    /// Clear auth credentials (logout).
    void clearCredentials();

    // API key/secret management
    void    setApiKey(const QString& key);
    void    setApiSecret(const QString& secret);
    void    setSessionKey(const QString& sk);
    QString apiKey() const;
    QString sessionKey() const;

signals:
    void enabledChanged();
    void authenticatedChanged();
    void scrobbleSubmitted(bool success, QString error);
    void nowPlayingUpdated(bool success);

private slots:
    void flushQueue();

private:
    QString generateApiSignature(const QHash<QString, QString>& params) const;
    void submitScrobble(const ScrobbleRecord& record);
    void submitNowPlaying(const ScrobbleRecord& record);

    mf::core::sources::HttpClient* http_ = nullptr;
    mf::core::services::SettingsControl* settings_ = nullptr;
    QTimer flushTimer_;
    QQueue<ScrobbleRecord> pendingQueue_;
    bool enabled_ = false;
};

} // namespace mf::core::services
