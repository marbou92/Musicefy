// DownloadService.h
// Downloads tracks from streaming sources to a local cache directory.
// Each download is tracked in-memory and reported via progress/complete
// callbacks. Cancel is supported. The service uses HttpClient for the
// actual file transfer; the source's session supplies the stream URL.

#pragma once

#include "../interfaces/IDownloadService.h"
#include "../models/MusicFile.h"

#include <QHash>
#include <QList>
#include <QObject>
#include <QString>

#include <memory>

#include "../sources/HttpClient.h"

namespace mf::core::services {

class DownloadService : public QObject,
                        public mf::core::interfaces::IDownloadService {
    Q_OBJECT
public:
    explicit DownloadService(QObject* parent = nullptr);
    DownloadService(mf::core::sources::HttpClient* http,
                    QString downloadDir,
                    QObject* parent = nullptr);
    ~DownloadService() override;

    void download(mf::core::models::MusicFile track, CompletionCallback onDone) override;
    void cancel(QString trackId) override;
    bool isDownloading(QString trackId) const override;
    bool isDownloaded(QString trackId) const override;
    QString localPathFor(QString trackId) const override;
    void removeDownload(QString trackId) override;

    // ── Configuration (read/write from the settings panel) ─────────
    QString downloadDir()    const { return downloadDir_; }
    void   setDownloadDir(const QString& d);
    int    activeCount()     const { return active_.size(); }
    int    completedCount()  const { return completed_.size(); }
    static QString defaultDownloadDir();

    /// List of all completed downloads (trackId -> localPath). Used by
    /// the downloads settings panel to show history.
    QHash<QString, QString> completedDownloads() const { return completed_; }

    /// Persist completed downloads to QSettings. Called automatically on
    /// completion and can be called manually before shutdown.
    void saveCompleted();

    /// Restore previously persisted completed downloads from QSettings.
    /// Called automatically on construction.
    void restoreCompleted();

    void setOnProgress(ProgressCallback cb) override { onProgress_ = std::move(cb); }
    void setOnCompletion(CompletionCallback cb) override { onCompletion_ = std::move(cb); }

signals:
    void progressQ(QString trackId, int percent);
    void completionQ(QString trackId, bool ok, QString localPath, QString errorMessage);

private:
    struct Job {
        QString trackId;
        QString localPath;
        CompletionCallback onDone;
    };

    mf::core::sources::HttpClient* http_ = nullptr;
    QString                        downloadDir_;
    QHash<QString, Job>            active_;
    QHash<QString, QString>        completed_; // trackId -> localPath
    ProgressCallback               onProgress_;
    CompletionCallback             onCompletion_;
};

} // namespace mf::core::services
