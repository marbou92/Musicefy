#include "DatabaseConfig.h"

#include <QCoreApplication>
#include <QDir>
#include <QStandardPaths>

namespace mf::core::database {

DatabaseConfig DatabaseConfig::defaultConfig() {
    DatabaseConfig c;
    QString dataDir = QStandardPaths::writableLocation(QStandardPaths::AppDataLocation);
    if (dataDir.isEmpty()) {
        dataDir = QDir::homePath() + QStringLiteral("/.musicefy");
    }
    QDir().mkpath(dataDir);
    c.setFilePath(dataDir + QStringLiteral("/musicefy.db"));

    QString migrationDir = QCoreApplication::applicationDirPath() + QStringLiteral("/migrations");
    if (!QDir(migrationDir).exists()) {
        migrationDir = dataDir + QStringLiteral("/migrations");
        QDir().mkpath(migrationDir);
    }
    c.setMigrationFiles({migrationDir});
    return c;
}

} // namespace mf::core::database
