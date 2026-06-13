#include "Database.h"

#include <QDateTime>
#include <QDebug>
#include <QFile>
#include <QFileInfo>
#include <QDir>
#include <QSqlError>
#include <QSqlRecord>
#include <QTextStream>
#include <QUuid>

namespace mf::core::database {

namespace {

QString readSqlFile(const QString& path) {
    QFile f(path);
    if (!f.open(QIODevice::ReadOnly | QIODevice::Text)) {
        return QString();
    }
    QTextStream in(&f);
    in.setCodec("UTF-8");
    return in.readAll();
}

} // namespace

Database::Database(DatabaseConfig config)
    : config_(std::move(config))
    , connectionName_(QStringLiteral("musicefy_") + QUuid::createUuid().toString(QUuid::WithoutBraces))
{
}

Database::~Database() {
    close();
}

bool Database::open() {
    if (open_) {
        return true;
    }
    connection_ = QSqlDatabase::addDatabase(QStringLiteral("QSQLITE"), connectionName_);
    if (config_.isInMemory()) {
        connection_.setDatabaseName(QStringLiteral(":memory:"));
    } else {
        connection_.setDatabaseName(config_.filePath());
    }
    if (!connection_.open()) {
        qWarning() << "Failed to open database:" << connection_.lastError().text();
        return false;
    }
    if (config_.foreignKeys()) {
        QSqlQuery q(connection_);
        if (!q.exec(QStringLiteral("PRAGMA foreign_keys = ON"))) {
            qWarning() << "Failed to enable foreign keys:" << q.lastError().text();
        }
    }
    connection_.exec(QStringLiteral("PRAGMA busy_timeout = %1")
                         .arg(config_.busyTimeout().count()));
    open_ = true;
    if (!config_.migrationFiles().isEmpty()) {
        if (!migrate()) {
            close();
            return false;
        }
    }
    return true;
}

void Database::close() {
    if (!open_) {
        return;
    }
    connection_.close();
    QSqlDatabase::removeDatabase(connectionName_);
    open_ = false;
}

bool Database::isOpen() const {
    return open_;
}

QSqlDatabase& Database::connection() {
    return connection_;
}

const QSqlDatabase& Database::connection() const {
    return connection_;
}

bool Database::execPrepared(QSqlQuery& query) {
    if (!query.exec()) {
        qWarning() << "SQL failed:" << query.lastQuery() << "err:" << query.lastError().text();
        return false;
    }
    return true;
}

int Database::schemaVersion() const {
    if (!open_) {
        return 0;
    }
    QSqlQuery q(connection_);
    if (!q.exec(QStringLiteral("PRAGMA user_version"))) {
        return 0;
    }
    if (q.next()) {
        return q.value(0).toInt();
    }
    return 0;
}

bool Database::migrate() {
    if (!open_) {
        return false;
    }

    QSqlQuery init(connection_);
    if (!init.exec(QStringLiteral("CREATE TABLE IF NOT EXISTS schema_migrations ("
                                  "version INTEGER PRIMARY KEY,"
                                  "applied_at INTEGER NOT NULL)"))) {
        qWarning() << "Failed to create schema_migrations:" << init.lastError().text();
        return false;
    }

    int current = schemaVersion();

    QStringList migrationFiles;
    for (const QString& dir : config_.migrationFiles()) {
        QDir d(dir);
        QStringList entries = d.entryList({QStringLiteral("*.sql")}, QDir::Files, QDir::Name);
        for (const QString& name : entries) {
            migrationFiles.append(dir + QStringLiteral("/") + name);
        }
    }

    int targetVersion = current;
    for (const QString& path : migrationFiles) {
        QFileInfo fi(path);
        QString stem = fi.baseName();
        int v = 0;
        for (QChar c : stem) {
            if (c.isDigit()) {
                v = v * 10 + (c.digitValue());
            } else {
                break;
            }
        }
        if (v <= 0 || v <= current) {
            continue;
        }
        QString sql = readSqlFile(path);
        if (sql.isEmpty()) {
            qWarning() << "Empty or missing migration file:" << path;
            return false;
        }

        if (!connection_.transaction()) {
            qWarning() << "Failed to begin transaction for migration" << v;
            return false;
        }
        QStringList statements = sql.split(QChar(';'), Qt::SkipEmptyParts);
        for (const QString& stmt : statements) {
            QString trimmed = stmt.trimmed();
            if (trimmed.isEmpty()) {
                continue;
            }
            QSqlQuery q(connection_);
            if (!q.exec(trimmed)) {
                qWarning() << "Failed migration" << v << "stmt:" << trimmed
                           << "err:" << q.lastError().text();
                connection_.rollback();
                return false;
            }
        }
        QSqlQuery setVer(connection_);
        setVer.prepare(QStringLiteral("INSERT INTO schema_migrations (version, applied_at) VALUES (?, ?)"));
        setVer.addBindValue(v);
        setVer.addBindValue(QDateTime::currentSecsSinceEpoch());
        if (!setVer.exec()) {
            qWarning() << "Failed to record migration" << v;
            connection_.rollback();
            return false;
        }
        if (!connection_.commit()) {
            qWarning() << "Failed to commit migration" << v;
            return false;
        }
        targetVersion = v;
    }

    if (targetVersion > current) {
        QSqlQuery bump(connection_);
        if (!bump.exec(QStringLiteral("PRAGMA user_version = %1").arg(targetVersion))) {
            qWarning() << "Failed to set user_version:" << bump.lastError().text();
            return false;
        }
    }
    return true;
}

} // namespace mf::core::database
