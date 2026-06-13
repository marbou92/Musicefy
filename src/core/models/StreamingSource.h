// StreamingSource.h
// Represents a configured streaming source (Local folder, Subsonic, YouTube, etc.).
// Port of Musicefy.Core.Models.StreamingSource.

#pragma once

#include <QHash>
#include <QString>

namespace mf::core::models {

class StreamingSource {
public:
    StreamingSource() = default;

    QString id() const { return id_; }
    void setId(QString v) { id_ = std::move(v); }

    QString name() const { return name_; }
    void setName(QString v) { name_ = std::move(v); }

    /// "Subsonic", "Local", "YouTube", or a custom extension type.
    QString type() const { return type_; }
    void setType(QString v) { type_ = std::move(v); }

    QString url() const { return url_; }
    void setUrl(QString v) { url_ = std::move(v); }

    QString username() const { return username_; }
    void setUsername(QString v) { username_ = std::move(v); }

    /// Password is stored as plain text in memory; persistence is
    /// expected to encrypt it (the source field is kept out of any
    /// serialized configuration dict).
    QString password() const { return password_; }
    void setPassword(QString v) { password_ = std::move(v); }

    bool isConnected() const { return isConnected_; }
    void setIsConnected(bool v) { isConnected_ = v; }

    QString clientVersion() const { return clientVersion_; }
    void setClientVersion(QString v) { clientVersion_ = std::move(v); }

    /// Provider-specific config. For Subsonic: url/username/password.
    /// For Local: folderPath. For YouTube: apiKey.
    const QHash<QString, QString>& configuration() const { return configuration_; }
    QHash<QString, QString>& mutableConfiguration() { return configuration_; }
    void setConfiguration(QHash<QString, QString> v) { configuration_ = std::move(v); }

    /// Copy legacy fields (url, username) into the Configuration dict.
    /// Does NOT copy password (which lives in the dedicated property).
    void ensureConfiguration();

    QString toDisplayString() const;

private:
    QString id_;
    QString name_;
    QString type_;
    QString url_;
    QString username_;
    QString password_;
    bool isConnected_ = false;
    QString clientVersion_ = QStringLiteral("1.0");
    QHash<QString, QString> configuration_;
};

} // namespace mf::core::models
