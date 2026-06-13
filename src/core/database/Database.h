#pragma once

#include <QObject>
#include <QSqlDatabase>
#include <QSqlQuery>

#include <functional>
#include <memory>
#include <string>

#include "DatabaseConfig.h"

namespace mf::core::database {

class Database {
public:
    explicit Database(DatabaseConfig config);
    ~Database();

    Database(const Database&) = delete;
    Database& operator=(const Database&) = delete;

    bool open();
    void close();
    bool isOpen() const;

    QSqlDatabase& connection();
    const QSqlDatabase& connection() const;

    /// Apply all migrations from the configured migration directory.
    bool migrate();

    /// Returns the current schema version, or 0 if not initialized.
    int schemaVersion() const;

    /// Execute a callback in a transaction. Rolls back on exception.
    template <typename Fn>
    bool inTransaction(Fn&& fn) {
        QSqlDatabase& db = connection();
        if (!db.transaction()) {
            return false;
        }
        try {
            fn(db);
        } catch (...) {
            db.rollback();
            throw;
        }
        if (!db.commit()) {
            return false;
        }
        return true;
    }

    /// Execute a parameterized statement.
    bool execPrepared(QSqlQuery& query);

    const DatabaseConfig& config() const { return config_; }

private:
    DatabaseConfig config_;
    QSqlDatabase connection_;
    QString connectionName_;
    bool open_ = false;
};

} // namespace mf::core::database
