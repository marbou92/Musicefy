// CrashLogger.h
// Lightweight crash/error logger that writes timestamped entries to a
// persistent log file. Survives even hard crashes (SIGSEGV, etc.)
// because the file is flushed before abort() is called.
//
// Log location: %LOCALAPPDATA%/Musicefy/error.log
// Rotation:     If the file exceeds 5 MB on startup, it is renamed to
//               error_<timestamp>.log and a fresh file is created.

#pragma once

#include <QMutex>
#include <QString>

#include <exception>

namespace mf::core::services {

class CrashLogger {
public:
    /// Singleton access — safe to call before QApplication exists.
    static CrashLogger& instance();

    /// Write a free-form message with timestamp.
    void write(const QString& message);

    /// Write an exception's what() + type info.
    void writeException(const std::exception& ex);

    /// Write a signal/SEH code (e.g. "SIGSEGV" or "0xC0000005").
    void writeSignal(const char* signalName);

    /// Flush the underlying file — call before abort().
    void flush();

    /// Return the full path of the current log file (for UI display).
    QString logFilePath() const;

private:
    CrashLogger();
    ~CrashLogger();
    CrashLogger(const CrashLogger&) = delete;
    CrashLogger& operator=(const CrashLogger&) = delete;

    void openLogFile();
    void rotateIfNeeded();
    QString timestamp() const;
    QString appDataPath() const;

    mutable QMutex mutex_;
    FILE* file_ = nullptr;
    QString filePath_;
    bool opened_ = false;
};

} // namespace mf::core::services
