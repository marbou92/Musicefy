// CrashLogger.cpp
// Dual-file crash/error logger. Writes to both:
//   1. %LOCALAPPDATA%/Musicefy/error.log  (primary)
//   2. <exe directory>/error.log           (fallback — always writable)
// If either path fails, the other still works.

#include "CrashLogger.h"

#include <QCoreApplication>
#include <QDateTime>
#include <QDir>
#include <QFileInfo>
#include <QStandardPaths>

#include <cstdio>

namespace mf::core::services {

// ── Singleton ──────────────────────────────────────────────────────────────

CrashLogger& CrashLogger::instance() {
    static CrashLogger s;
    return s;
}

// ── Construction / destruction ─────────────────────────────────────────────

CrashLogger::CrashLogger() {
    openLogFiles();
    rotateIfNeeded();

    // Write session header to whichever files opened.
    const QString hdr = QStringLiteral("\n=== Musicefy session started %1 ===\n")
                            .arg(timestamp());
    if (primaryFile_) {
        std::fwrite(hdr.toUtf8().constData(), 1, hdr.toUtf8().size(), primaryFile_);
        std::fflush(primaryFile_);
    }
    if (fallbackFile_) {
        std::fwrite(hdr.toUtf8().constData(), 1, hdr.toUtf8().size(), fallbackFile_);
        std::fflush(fallbackFile_);
    }
}

CrashLogger::~CrashLogger() {
    flush();
    if (primaryFile_)  { std::fclose(primaryFile_);  primaryFile_  = nullptr; }
    if (fallbackFile_) { std::fclose(fallbackFile_); fallbackFile_ = nullptr; }
}

// ── Public API ─────────────────────────────────────────────────────────────

void CrashLogger::write(const QString& message) {
    QMutexLocker lock(&mutex_);
    const QByteArray line = QByteArray("[") + timestamp().toUtf8() + "] "
                            + message.toUtf8() + "\n";
    if (primaryFile_)  { std::fwrite(line.constData(), 1, line.size(), primaryFile_);  std::fflush(primaryFile_); }
    if (fallbackFile_) { std::fwrite(line.constData(), 1, line.size(), fallbackFile_); std::fflush(fallbackFile_); }
}

void CrashLogger::writeException(const std::exception& ex) {
    QMutexLocker lock(&mutex_);
    const QByteArray block =
        QByteArray("\n=== EXCEPTION ===\nType:  ") + typeid(ex).name()
        + "\nWhat: " + ex.what()
        + "\nTime:  " + timestamp().toUtf8()
        + "\n==================\n";
    if (primaryFile_)  { std::fwrite(block.constData(), 1, block.size(), primaryFile_);  std::fflush(primaryFile_); }
    if (fallbackFile_) { std::fwrite(block.constData(), 1, block.size(), fallbackFile_); std::fflush(fallbackFile_); }
}

void CrashLogger::writeSignal(const char* signalName) {
    QMutexLocker lock(&mutex_);
    const QByteArray block =
        QByteArray("\n=== CRASH (") + signalName + ") ===\nTime: " + timestamp().toUtf8() + "\n";
    if (primaryFile_)  { std::fwrite(block.constData(), 1, block.size(), primaryFile_);  std::fflush(primaryFile_); }
    if (fallbackFile_) { std::fwrite(block.constData(), 1, block.size(), fallbackFile_); std::fflush(fallbackFile_); }
}

void CrashLogger::flush() {
    QMutexLocker lock(&mutex_);
    if (primaryFile_)  std::fflush(primaryFile_);
    if (fallbackFile_) std::fflush(fallbackFile_);
}

QString CrashLogger::logFilePath() const {
    return primaryPath_;
}

QString CrashLogger::fallbackLogPath() const {
    return fallbackPath_;
}

// ── Private helpers ────────────────────────────────────────────────────────

QString CrashLogger::appDataPath() const {
    QString path = QStandardPaths::writableLocation(QStandardPaths::AppLocalDataLocation);
    if (path.isEmpty()) {
#ifdef Q_OS_WIN
        path = QDir::homePath() + QStringLiteral("/AppData/Local/Musicefy");
#else
        path = QDir::homePath() + QStringLiteral("/.local/share/Musicefy");
#endif
    }
    return path;
}

void CrashLogger::openLogFiles() {
    // ── Primary: %LOCALAPPDATA%/Musicefy/error.log ──
    QDir primaryDir(appDataPath());
    if (!primaryDir.exists()) primaryDir.mkpath(QStringLiteral("."));
    primaryPath_ = primaryDir.filePath(QStringLiteral("error.log"));
    primaryFile_ = std::fopen(primaryPath_.toUtf8().constData(), "a");

    // ── Fallback: next to the executable ──
    QString exeDir = QCoreApplication::applicationDirPath();
    if (exeDir.isEmpty()) {
        // May be called before QApplication — fall back to argv[0].
        // Just use CWD in the worst case.
        exeDir = QDir::currentPath();
    }
    QDir fbDir(exeDir);
    fallbackPath_ = fbDir.filePath(QStringLiteral("error.log"));
    fallbackFile_ = std::fopen(fallbackPath_.toUtf8().constData(), "a");
}

void CrashLogger::rotateIfNeeded() {
    // Rotate primary if > 5 MB.
    if (primaryFile_) {
        QFileInfo info(primaryPath_);
        if (info.exists() && info.size() > 5 * 1024 * 1024) {
            std::fclose(primaryFile_);
            primaryFile_ = nullptr;
            QDir dir(appDataPath());
            QString rotated = dir.filePath(
                QStringLiteral("error_%1.log")
                    .arg(QDateTime::currentDateTime().toString(QStringLiteral("yyyyMMdd_HHmmss"))));
            QFile::rename(primaryPath_, rotated);
            primaryFile_ = std::fopen(primaryPath_.toUtf8().constData(), "a");
        }
    }
    // Rotate fallback if > 5 MB.
    if (fallbackFile_) {
        QFileInfo info(fallbackPath_);
        if (info.exists() && info.size() > 5 * 1024 * 1024) {
            std::fclose(fallbackFile_);
            fallbackFile_ = nullptr;
            QDir dir(QCoreApplication::applicationDirPath().isEmpty()
                     ? QDir::currentPath()
                     : QCoreApplication::applicationDirPath());
            QString rotated = dir.filePath(
                QStringLiteral("error_%1.log")
                    .arg(QDateTime::currentDateTime().toString(QStringLiteral("yyyyMMdd_HHmmss"))));
            QFile::rename(fallbackPath_, rotated);
            fallbackFile_ = std::fopen(fallbackPath_.toUtf8().constData(), "a");
        }
    }
}

QString CrashLogger::timestamp() const {
    return QDateTime::currentDateTime().toString(
        QStringLiteral("yyyy-MM-dd HH:mm:ss.zzz"));
}

} // namespace mf::core::services
