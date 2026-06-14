// CrashLogger.cpp
// Implementation of the crash/error logger.

#include "CrashLogger.h"

#include <QDateTime>
#include <QDir>
#include <QFileInfo>
#include <QStandardPaths>

#include <cstdio>
#include <ctime>

namespace mf::core::services {

// ── Singleton ──────────────────────────────────────────────────────────────

CrashLogger& CrashLogger::instance() {
    static CrashLogger s;
    return s;
}

// ── Construction / destruction ─────────────────────────────────────────────

CrashLogger::CrashLogger() {
    openLogFile();
}

CrashLogger::~CrashLogger() {
    flush();
    if (file_) {
        std::fclose(file_);
        file_ = nullptr;
    }
}

// ── Public API ─────────────────────────────────────────────────────────────

void CrashLogger::write(const QString& message) {
    QMutexLocker lock(&mutex_);
    if (!file_) return;

    std::fprintf(file_, "[%s] %s\n",
                 qPrintable(timestamp()),
                 qPrintable(message));
}

void CrashLogger::writeException(const std::exception& ex) {
    QMutexLocker lock(&mutex_);
    if (!file_) return;

    std::fprintf(file_, "\n=== EXCEPTION ===\n");
    std::fprintf(file_, "Time:  %s\n",  qPrintable(timestamp()));
    std::fprintf(file_, "Type:  %s\n",  typeid(ex).name());
    std::fprintf(file_, "What:  %s\n",  ex.what());
    std::fprintf(file_, "==================\n\n");
}

void CrashLogger::writeSignal(const char* signalName) {
    QMutexLocker lock(&mutex_);
    if (!file_) return;

    std::fprintf(file_, "\n=== CRASH (%s) ===\n", signalName);
    std::fprintf(file_, "Time: %s\n", qPrintable(timestamp()));
}

void CrashLogger::flush() {
    QMutexLocker lock(&mutex_);
    if (file_) {
        std::fflush(file_);
    }
}

QString CrashLogger::logFilePath() const {
    return filePath_;
}

// ── Private helpers ────────────────────────────────────────────────────────

QString CrashLogger::appDataPath() const {
    QString path = QStandardPaths::writableLocation(QStandardPaths::AppLocalDataLocation);
    if (path.isEmpty()) {
        // Fallback for pre-QApplication construction.
#ifdef Q_OS_WIN
        path = QDir::homePath() + QStringLiteral("/AppData/Local/Musicefy");
#else
        path = QDir::homePath() + QStringLiteral("/.local/share/Musicefy");
#endif
    }
    return path;
}

void CrashLogger::openLogFile() {
    QDir dir(appDataPath());
    if (!dir.exists()) {
        dir.mkpath(QStringLiteral("."));
    }

    filePath_ = dir.filePath(QStringLiteral("error.log"));
    rotateIfNeeded();

    file_ = std::fopen(filePath_.toUtf8().constData(), "a");
    if (file_) {
        opened_ = true;
        std::fprintf(file_, "\n--- Musicefy session started %s ---\n",
                     qPrintable(timestamp()));
        std::fflush(file_);
    }
}

void CrashLogger::rotateIfNeeded() {
    QFileInfo info(filePath_);
    if (!info.exists()) return;

    // 5 MB threshold.
    if (info.size() > 5 * 1024 * 1024) {
        QDir dir(appDataPath());
        QString rotated = dir.filePath(
            QStringLiteral("error_%1.log")
                .arg(QDateTime::currentDateTime().toString(QStringLiteral("yyyyMMdd_HHmmss"))));
        QFile::rename(filePath_, rotated);
    }
}

QString CrashLogger::timestamp() const {
    return QDateTime::currentDateTime().toString(
        QStringLiteral("yyyy-MM-dd HH:mm:ss.zzz"));
}

} // namespace mf::core::services
