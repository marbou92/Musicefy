// YtDlpProcess.cpp — see YtDlpProcess.h.

#include "YtDlpProcess.h"

#include <QByteArray>
#include <QByteArrayList>
#include <QCoreApplication>
#include <QFileInfo>
#include <QJsonDocument>
#include <QJsonObject>
#include <QProcess>
#include <QStandardPaths>
#include <QTimer>

#include <utility>

namespace mf::core::sources::youtube {

namespace {

constexpr int kStartupTimeoutMs  = 5'000;
constexpr int kFinishTimeoutMs   = 30'000;

QString tryFile(const QString& path) {
    if (path.isEmpty()) return QString();
    const QFileInfo fi(path);
    if (fi.exists() && fi.isFile() && fi.isExecutable()) {
        return fi.absoluteFilePath();
    }
    return QString();
}

} // namespace

YtDlpProcess::YtDlpProcess(QObject* parent)
    : QObject(parent) {
}

YtDlpProcess::~YtDlpProcess() {
    if (process_ && process_->state() != QProcess::NotRunning) {
        process_->kill();
        process_->waitForFinished(1'000);
    }
}

QString YtDlpProcess::findExecutable(const QString& hint) {
    if (const QString hit = tryFile(hint); !hit.isEmpty()) return hit;

#ifdef Q_OS_WIN
    const QString exeName = QStringLiteral("yt-dlp.exe");
#else
    const QString exeName = QStringLiteral("yt-dlp");
#endif

    const QString appDir = QCoreApplication::applicationDirPath();
    for (const QString& rel : {
             QStringLiteral("/localdata/tools/%1").arg(exeName),
             QStringLiteral("/tools/%1").arg(exeName),
             QStringLiteral("/%1").arg(exeName),
         }) {
        if (const QString hit = tryFile(appDir + rel); !hit.isEmpty()) return hit;
    }

    if (const QString hit = QStandardPaths::findExecutable(exeName);
        !hit.isEmpty()) {
        return hit;
    }

    return QString();
}

bool YtDlpProcess::isAvailable(const QString& hint) {
    return !findExecutable(hint).isEmpty();
}

void YtDlpProcess::setExecutable(QString path) {
    executable_ = std::move(path);
}

void YtDlpProcess::resolveStreamUrl(const QString& videoUrl,
                                    StringCallback cb) {
    if (fakeOutput_.active) {
        lastArgs_.clear();
        lastError_.clear();
        if (cb) cb(fakeOutput_.streamUrl, fakeOutput_.error);
        return;
    }
    QStringList args {
        QStringLiteral("--no-warnings"),
        QStringLiteral("--no-playlist"),
        QStringLiteral("--no-progress"),
        QStringLiteral("--get-url"),
        QStringLiteral("-f"),
        QStringLiteral("bestaudio/best"),
    };
    if (videoUrl.startsWith(QStringLiteral("ytsearch:")) ||
        videoUrl.startsWith(QStringLiteral("https://"))) {
        args << videoUrl;
    } else {
        args << QStringLiteral("https://www.youtube.com/watch?v=%1").arg(videoUrl);
    }
    startProcess(args, std::move(cb));
}

void YtDlpProcess::search(QString query, int limit, SearchCallback cb) {
    if (fakeOutput_.active) {
        lastArgs_.clear();
        lastError_.clear();
        if (cb) cb(fakeOutput_.searchResults, fakeOutput_.error);
        return;
    }
    const int capped = qMax(1, qMin(50, limit));
    QStringList args {
        QStringLiteral("--no-warnings"),
        QStringLiteral("--no-playlist"),
        QStringLiteral("--flat-playlist"),
        QStringLiteral("--dump-json"),
    };
    args << QStringLiteral("ytsearch%1:%2").arg(capped).arg(query);
    startProcessMulti(args, std::move(cb));
}

void YtDlpProcess::getVersion(VersionCallback cb) {
    if (fakeOutput_.active) {
        lastArgs_.clear();
        lastError_.clear();
        if (cb) cb(fakeOutput_.version, fakeOutput_.error);
        return;
    }
    QStringList args { QStringLiteral("--version") };
    startProcessVersion(args, std::move(cb));
}

void YtDlpProcess::startProcess(const QStringList& args, StringCallback cb) {
    lastArgs_  = args;
    lastError_.clear();

    if (executable_.isEmpty()) {
        const QString hit = findExecutable();
        if (hit.isEmpty()) {
            lastError_ = QStringLiteral("yt-dlp executable not found");
            if (cb) cb(QString(), lastError_);
            return;
        }
        executable_ = hit;
    }

    if (process_ && process_->state() != QProcess::NotRunning) {
        lastError_ = QStringLiteral("yt-dlp already running");
        if (cb) cb(QString(), lastError_);
        return;
    }

    process_ = new QProcess(this);
    process_->setProgram(executable_);
    process_->setArguments(args);

    QPointer<QProcess> proc = process_;
    auto finished = [this, proc, cb = std::move(cb)](int exitCode,
                                                     QProcess::ExitStatus) {
        if (!proc) return;
        const QByteArray out = proc->readAllStandardOutput();
        const QByteArray err = proc->readAllStandardError();
        proc->deleteLater();
        if (exitCode != 0) {
            lastError_ = QString::fromUtf8(err).trimmed();
            if (lastError_.isEmpty()) {
                lastError_ = QStringLiteral("yt-dlp exited with code %1").arg(exitCode);
            }
            if (cb) cb(QString(), lastError_);
            return;
        }
        const QString text = QString::fromUtf8(out);
        const QStringList lines = text.split(QLatin1Char('\n'), Qt::SkipEmptyParts);
        for (const QString& l : lines) {
            const QString trimmed = l.trimmed();
            if (!trimmed.isEmpty() &&
                (trimmed.startsWith(QStringLiteral("http://")) ||
                 trimmed.startsWith(QStringLiteral("https://")))) {
                if (cb) cb(trimmed, QString());
                return;
            }
        }
        if (cb) cb(QString(),
                   QStringLiteral("yt-dlp produced no stream URL (stdout: %1)")
                       .arg(text.left(200)));
    };

    connect(process_.data(), QOverload<int, QProcess::ExitStatus>::of(&QProcess::finished),
            this, finished);

    process_->start();
    if (!process_->waitForStarted(kStartupTimeoutMs)) {
        lastError_ = QStringLiteral("yt-dlp failed to start: %1")
                         .arg(process_->errorString());
        process_->deleteLater();
        process_.clear();
        if (cb) cb(QString(), lastError_);
        return;
    }

    QTimer::singleShot(kFinishTimeoutMs, this, [this, proc]() {
        if (proc && proc->state() != QProcess::NotRunning) {
            proc->kill();
            proc->waitForFinished(1'000);
        }
    });
}

void YtDlpProcess::startProcessMulti(const QStringList& args, SearchCallback cb) {
    lastArgs_  = args;
    lastError_.clear();

    if (executable_.isEmpty()) {
        const QString hit = findExecutable();
        if (hit.isEmpty()) {
            lastError_ = QStringLiteral("yt-dlp executable not found");
            if (cb) cb({}, lastError_);
            return;
        }
        executable_ = hit;
    }

    if (process_ && process_->state() != QProcess::NotRunning) {
        lastError_ = QStringLiteral("yt-dlp already running");
        if (cb) cb({}, lastError_);
        return;
    }

    process_ = new QProcess(this);
    process_->setProgram(executable_);
    process_->setArguments(args);

    QPointer<QProcess> proc = process_;
    auto finished = [this, proc, cb = std::move(cb)](int exitCode,
                                                     QProcess::ExitStatus) {
        if (!proc) return;
        const QByteArray out = proc->readAllStandardOutput();
        const QByteArray err = proc->readAllStandardError();
        proc->deleteLater();
        if (exitCode != 0) {
            lastError_ = QString::fromUtf8(err).trimmed();
            if (lastError_.isEmpty()) {
                lastError_ = QStringLiteral("yt-dlp exited with code %1").arg(exitCode);
            }
            if (cb) cb({}, lastError_);
            return;
        }
        QList<YtDlpProcess::SearchEntry> entries;
        const QByteArrayList jsonLines = out.split('\n');
        for (const QByteArray& raw : jsonLines) {
            const QByteArray line = raw.trimmed();
            if (line.isEmpty()) continue;
            const QJsonDocument doc = QJsonDocument::fromJson(line);
            if (!doc.isObject()) continue;
            const QJsonObject obj = doc.object();
            YtDlpProcess::SearchEntry e;
            e.id              = obj.value(QStringLiteral("id")).toString();
            e.title           = obj.value(QStringLiteral("title")).toString();
            e.uploader        = obj.value(QStringLiteral("uploader")).toString();
            if (e.uploader.isEmpty()) e.uploader = obj.value(QStringLiteral("channel")).toString();
            e.durationSeconds = static_cast<qint64>(
                obj.value(QStringLiteral("duration")).toDouble(0.0));
            if (!e.id.isEmpty()) entries.append(std::move(e));
        }
        if (cb) cb(entries, QString());
    };

    connect(process_.data(), QOverload<int, QProcess::ExitStatus>::of(&QProcess::finished),
            this, finished);

    process_->start();
    if (!process_->waitForStarted(kStartupTimeoutMs)) {
        lastError_ = QStringLiteral("yt-dlp failed to start: %1")
                         .arg(process_->errorString());
        process_->deleteLater();
        process_.clear();
        if (cb) cb({}, lastError_);
        return;
    }

    QTimer::singleShot(kFinishTimeoutMs, this, [this, proc]() {
        if (proc && proc->state() != QProcess::NotRunning) {
            proc->kill();
            proc->waitForFinished(1'000);
        }
    });
}

void YtDlpProcess::startProcessVersion(const QStringList& args, VersionCallback cb) {
    lastArgs_  = args;
    lastError_.clear();

    if (executable_.isEmpty()) {
        const QString hit = findExecutable();
        if (hit.isEmpty()) {
            lastError_ = QStringLiteral("yt-dlp executable not found");
            if (cb) cb(QString(), lastError_);
            return;
        }
        executable_ = hit;
    }

    if (process_ && process_->state() != QProcess::NotRunning) {
        lastError_ = QStringLiteral("yt-dlp already running");
        if (cb) cb(QString(), lastError_);
        return;
    }

    process_ = new QProcess(this);
    process_->setProgram(executable_);
    process_->setArguments(args);

    QPointer<QProcess> proc = process_;
    auto finished = [this, proc, cb = std::move(cb)](int exitCode,
                                                     QProcess::ExitStatus) {
        if (!proc) return;
        const QByteArray out = proc->readAllStandardOutput();
        const QByteArray err = proc->readAllStandardError();
        proc->deleteLater();
        if (exitCode != 0) {
            lastError_ = QString::fromUtf8(err).trimmed();
            if (cb) cb(QString(), lastError_);
            return;
        }
        const QString ver = QString::fromUtf8(out).trimmed();
        if (cb) cb(ver, QString());
    };

    connect(process_.data(), QOverload<int, QProcess::ExitStatus>::of(&QProcess::finished),
            this, finished);

    process_->start();
    if (!process_->waitForStarted(kStartupTimeoutMs)) {
        lastError_ = QStringLiteral("yt-dlp failed to start: %1")
                         .arg(process_->errorString());
        process_->deleteLater();
        process_.clear();
        if (cb) cb(QString(), lastError_);
        return;
    }
}

} // namespace mf::core::sources::youtube
