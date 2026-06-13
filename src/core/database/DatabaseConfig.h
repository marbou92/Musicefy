#pragma once

#include <QString>
#include <QStringList>

#include <chrono>

namespace mf::core::database {

class DatabaseConfig {
public:
    static DatabaseConfig defaultConfig();

    QString filePath() const { return filePath_; }
    void setFilePath(QString v) { filePath_ = std::move(v); }

    bool isInMemory() const { return inMemory_; }
    void setInMemory(bool v) { inMemory_ = v; }

    QStringList migrationFiles() const { return migrationFiles_; }
    void setMigrationFiles(QStringList v) { migrationFiles_ = std::move(v); }

    /// Enable foreign keys (default: on).
    bool foreignKeys() const { return foreignKeys_; }
    void setForeignKeys(bool v) { foreignKeys_ = v; }

    /// Busy timeout for write transactions.
    std::chrono::milliseconds busyTimeout() const { return busyTimeout_; }
    void setBusyTimeout(std::chrono::milliseconds v) { busyTimeout_ = v; }

private:
    QString filePath_ = QStringLiteral("musicefy.db");
    bool inMemory_ = false;
    QStringList migrationFiles_;
    bool foreignKeys_ = true;
    std::chrono::milliseconds busyTimeout_{5000};
};

} // namespace mf::core::database
