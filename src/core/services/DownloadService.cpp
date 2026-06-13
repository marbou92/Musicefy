// DownloadService.cpp
// See header for design notes. This implementation focuses on the
// orchestration: jobs are tracked, files are written to a downloads
// directory, and the HttpClient handles the byte transfer. The real
// implementation needs the source's session to mint a stream URL; the
// DownloadService accepts a MusicFile whose sourceUri is already a
// playable URL (e.g., a Subsonic stream URL).

#include "DownloadService.h"

#include "../sources/HttpClient.h"

#include <QDir>
#include <QFile>
#include <QFileInfo>
#include <QSettings>
#include <QStandardPaths>
#include <QUuid>

namespace mf::core::services {

using mf::core::interfaces::IDownloadService;
using mf::core::models::MusicFile;

namespace {
constexpr const char* kCompletedKey = "downloads/completed";
}

DownloadService::DownloadService(QObject* parent)
    : QObject(parent)
    , http_(new mf::core::sources::HttpClient())
{
    downloadDir_ = defaultDownloadDir();
    QDir().mkpath(downloadDir_);
    restoreCompleted();
}

void DownloadService::setDownloadDir(const QString& d) {
    if (d == downloadDir_) return;
    downloadDir_ = d;
    QDir().mkpath(downloadDir_);
}

QString DownloadService::defaultDownloadDir() {
    // Prefer the user's Music folder (Win 7+ honours XDG-style fallbacks
    // via QStandardPaths::MusicLocation). Falls back to the app cache
    // dir, then to ~/.musicefy/downloads.
    QString music = QStandardPaths::writableLocation(QStandardPaths::MusicLocation);
    if (!music.isEmpty()) {
        return music + QStringLiteral("/Musicefy/Downloads");
    }
    QString cache = QStandardPaths::writableLocation(QStandardPaths::CacheLocation);
    if (!cache.isEmpty()) {
        return cache + QStringLiteral("/downloads");
    }
    return QDir::homePath() + QStringLiteral("/.musicefy/downloads");
}

DownloadService::DownloadService(mf::core::sources::HttpClient* http,
                                 QString downloadDir,
                                 QObject* parent)
    : QObject(parent)
    , http_(http)
    , downloadDir_(std::move(downloadDir))
{
    QDir().mkpath(downloadDir_);
    restoreCompleted();
}

DownloadService::~DownloadService() = default;

QString DownloadService::localPathFor(QString trackId) const {
    if (completed_.contains(trackId)) {
        return completed_.value(trackId);
    }
    if (active_.contains(trackId)) {
        return active_.value(trackId).localPath;
    }
    return {};
}

bool DownloadService::isDownloading(QString trackId) const {
    return active_.contains(trackId);
}

bool DownloadService::isDownloaded(QString trackId) const {
    return completed_.contains(trackId);
}

void DownloadService::cancel(QString trackId) {
    auto it = active_.find(trackId);
    if (it == active_.end()) return;
    if (http_) {
        // http_ doesn't know per-request tags here; cancel-all for this
        // track is a no-op in the stub.
    }
    QFile::remove(it->localPath);
    active_.erase(it);
}

void DownloadService::removeDownload(QString trackId) {
    QString p = completed_.value(trackId);
    if (!p.isEmpty()) {
        QFile::remove(p);
    }
    completed_.remove(trackId);
    saveCompleted();
}

void DownloadService::download(MusicFile track, CompletionCallback onDone) {
    QString trackId = track.id().isEmpty() ? track.filePath() : track.id();
    if (trackId.isEmpty()) {
        if (onDone) onDone(trackId, false, {}, QStringLiteral("Track has no id"));
        if (onCompletion_) onCompletion_(trackId, false, {}, QStringLiteral("Track has no id"));
        emit completionQ(trackId, false, {}, QStringLiteral("Track has no id"));
        return;
    }
    QString url = track.sourceUri().isEmpty() ? track.filePath() : track.sourceUri();
    if (url.isEmpty()) {
        if (onDone) onDone(trackId, false, {}, QStringLiteral("Track has no source URL"));
        return;
    }
    QString suffix = QFileInfo(track.filePath()).suffix();
    if (suffix.isEmpty()) suffix = QStringLiteral("mp3");
    QString localPath = downloadDir_ + QStringLiteral("/") + trackId + QStringLiteral(".") + suffix;
    QFile::remove(localPath); // start clean

    Job j;
    j.trackId   = trackId;
    j.localPath = localPath;
    j.onDone    = std::move(onDone);
    active_.insert(trackId, j);

    if (!http_) {
        if (j.onDone) j.onDone(trackId, false, {}, QStringLiteral("No HTTP client"));
        active_.remove(trackId);
        return;
    }

    mf::core::sources::HttpRequest req;
    req.url = url;
    req.timeoutMs = 300'000; // 5 min ceiling
    http_->get(req, [this, trackId](mf::core::sources::HttpResponse resp) {
        Job job = active_.value(trackId);
        active_.remove(trackId);
        if (!resp.ok()) {
            if (job.onDone) job.onDone(trackId, false, {}, resp.errorMessage);
            if (onCompletion_) onCompletion_(trackId, false, {}, resp.errorMessage);
            emit completionQ(trackId, false, {}, resp.errorMessage);
            return;
        }
        QFile f(job.localPath);
        if (!f.open(QIODevice::WriteOnly)) {
            if (job.onDone) job.onDone(trackId, false, {}, f.errorString());
            if (onCompletion_) onCompletion_(trackId, false, {}, f.errorString());
            emit completionQ(trackId, false, {}, f.errorString());
            return;
        }
        f.write(resp.body);
        f.close();
        completed_.insert(trackId, job.localPath);
        saveCompleted();
        if (onProgress_) onProgress_(trackId, 100);
        emit progressQ(trackId, 100);
        if (job.onDone) job.onDone(trackId, true, job.localPath, {});
        if (onCompletion_) onCompletion_(trackId, true, job.localPath, {});
        emit completionQ(trackId, true, job.localPath, {});
    });
}

void DownloadService::saveCompleted() {
    QSettings s;
    QStringList entries;
    entries.reserve(completed_.size());
    for (auto it = completed_.constBegin(); it != completed_.constEnd(); ++it) {
        // Format: "trackId|localPath"
        entries << QStringLiteral("%1|%2").arg(it.key(), it.value());
    }
    s.setValue(QLatin1String(kCompletedKey), entries);
    s.sync();
}

void DownloadService::restoreCompleted() {
    QSettings s;
    QStringList entries = s.value(QLatin1String(kCompletedKey)).toStringList();
    for (const QString& entry : entries) {
        int sep = entry.indexOf(QLatin1Char('|'));
        if (sep < 0) continue;
        QString trackId = entry.left(sep);
        QString localPath = entry.mid(sep + 1);
        if (!trackId.isEmpty() && !localPath.isEmpty()) {
            completed_.insert(trackId, localPath);
        }
    }
}

} // namespace mf::core::services