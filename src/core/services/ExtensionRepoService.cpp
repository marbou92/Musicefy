// ExtensionRepoService.cpp
// See header for design notes.

#include "ExtensionRepoService.h"

#include "../sources/HttpClient.h"
#include "../services/SettingsControl.h"

#include <QJsonArray>
#include <QJsonDocument>
#include <QJsonObject>
#include <QUrl>

namespace mf::core::services {

namespace {
constexpr const char* kReposKey    = "extensions/repos";
constexpr const char* kAutoKey     = "extensions/auto_refresh";
constexpr const char* kIntervalKey = "extensions/refresh_hours";
constexpr const char* kManifestPath = "/manifest.json";
constexpr int kDefaultIntervalHours = 12;
} // anonymous namespace

using mf::core::sources::HttpClient;
using mf::core::sources::HttpRequest;
using mf::core::sources::HttpResponse;

// ── Construction / Destruction ─────────────────────────────────────

ExtensionRepoService::ExtensionRepoService(HttpClient*     http,
                                           SettingsControl* settings,
                                           QObject*         parent)
    : QObject(parent)
    , http_(http)
    , settings_(settings)
{
    loadRepoUrls();

    autoRefreshTimer_.setSingleShot(true);
    connect(&autoRefreshTimer_, &QTimer::timeout,
            this, &ExtensionRepoService::onAutoRefreshTimer);

    if (settings_) {
        bool autoOn = settings_->getOrDefault<bool>(
            QString::fromLatin1(kAutoKey), true);
        if (autoOn) {
            startAutoRefresh();
        }
    }
}

ExtensionRepoService::~ExtensionRepoService() = default;

// ── Public slots ───────────────────────────────────────────────────

void ExtensionRepoService::addRepo(const QString& url)
{
    QString trimmed = url.trimmed();
    if (!isValidUrl(trimmed)) return;

    for (const QString& existing : repoUrls_) {
        if (existing.compare(trimmed, Qt::CaseInsensitive) == 0) {
            return;
        }
    }

    repoUrls_.append(trimmed);
    saveRepoUrls();
    emit repoUrlsChanged();
}

void ExtensionRepoService::removeRepo(const QString& url)
{
    bool removed = false;
    for (int i = repoUrls_.size() - 1; i >= 0; --i) {
        if (repoUrls_[i].compare(url, Qt::CaseInsensitive) == 0) {
            repoUrls_.removeAt(i);
            removed = true;
        }
    }
    if (!removed) return;

    repoManifests_.remove(url);

    QList<mf::core::models::ExtensionManifest> merged;
    QHash<QString, mf::core::models::ExtensionManifest> seen;
    for (auto it = repoManifests_.constBegin(); it != repoManifests_.constEnd(); ++it) {
        for (const mf::core::models::ExtensionManifest& m : it.value()) {
            if (!seen.contains(m.id())) {
                seen.insert(m.id(), m);
                merged.append(m);
            }
        }
    }
    manifests_ = merged;

    saveRepoUrls();
    emit repoUrlsChanged();
    emit manifestsChanged();
}

void ExtensionRepoService::refresh()
{
    if (refreshing_) return;
    if (repoUrls_.isEmpty()) return;
    if (!http_) return;

    refreshing_ = true;
    emit refreshingChanged();

    pendingFetches_ = repoUrls_.size();

    for (const QString& repoUrl : repoUrls_) {
        HttpRequest req;
        req.url      = repoUrl + QString::fromLatin1(kManifestPath);
        req.method   = QByteArrayLiteral("GET");
        req.timeoutMs = 30000;

        http_->get(req, [this, repoUrl](HttpResponse resp) {
            if (resp.ok()) {
                onRepoFetched(repoUrl, resp.body);
            } else {
                onRepoFetchError(repoUrl, resp.errorMessage);
            }
        });
    }
}

void ExtensionRepoService::startAutoRefresh()
{
    if (!settings_) return;

    int hours = settings_->getOrDefault<int>(
        QString::fromLatin1(kIntervalKey), kDefaultIntervalHours);
    if (hours < 1) hours = 1;

    autoRefreshTimer_.setInterval(static_cast<qint64>(hours) * 3600 * 1000);
    autoRefreshTimer_.start();

    refresh();
}

void ExtensionRepoService::stopAutoRefresh()
{
    autoRefreshTimer_.stop();
}

// ── Private slots ──────────────────────────────────────────────────

void ExtensionRepoService::onRepoFetched(const QString& repoUrl,
                                         const QByteArray& data)
{
    QList<mf::core::models::ExtensionManifest> parsed = parseManifestJson(data);
    mergeManifests(repoUrl, parsed);

    --pendingFetches_;
    if (pendingFetches_ <= 0) {
        pendingFetches_ = 0;
        refreshing_ = false;
        emit refreshingChanged();
        emit refreshFinished(manifests_.size(), parsed.size());
        emit manifestsChanged();

        if (autoRefreshTimer_.isActive()) {
            autoRefreshTimer_.start();
        }
    }
}

void ExtensionRepoService::onRepoFetchError(const QString& repoUrl,
                                            const QString& error)
{
    emit refreshError(repoUrl, error);

    --pendingFetches_;
    if (pendingFetches_ <= 0) {
        pendingFetches_ = 0;
        refreshing_ = false;
        emit refreshingChanged();
        emit refreshFinished(manifests_.size(), 0);

        if (autoRefreshTimer_.isActive()) {
            autoRefreshTimer_.start();
        }
    }
}

void ExtensionRepoService::onAutoRefreshTimer()
{
    refresh();
}

// ── Private helpers ────────────────────────────────────────────────

void ExtensionRepoService::loadRepoUrls()
{
    repoUrls_.clear();

    if (settings_ && settings_->contains(QString::fromLatin1(kReposKey))) {
        repoUrls_ = settings_->getOrDefault<QStringList>(
            QString::fromLatin1(kReposKey), QStringList{});
    }

    if (repoUrls_.isEmpty()) {
        repoUrls_ << QStringLiteral("https://github.com/MarBou/Musicefy-extensions");
        saveRepoUrls();
    }
}

void ExtensionRepoService::saveRepoUrls()
{
    if (!settings_) return;
    settings_->set(QString::fromLatin1(kReposKey), repoUrls_);
    settings_->sync();
}

void ExtensionRepoService::mergeManifests(
    const QString& repoUrl,
    const QList<mf::core::models::ExtensionManifest>& parsed)
{
    repoManifests_[repoUrl] = parsed;

    QList<mf::core::models::ExtensionManifest> merged;
    QHash<QString, mf::core::models::ExtensionManifest> seen;

    for (const QString& url : repoUrls_) {
        auto it = repoManifests_.find(url);
        if (it == repoManifests_.end()) continue;
        for (const mf::core::models::ExtensionManifest& m : it.value()) {
            if (!m.id().isEmpty() && !seen.contains(m.id())) {
                seen.insert(m.id(), m);
                merged.append(m);
            }
        }
    }

    manifests_ = merged;
}

bool ExtensionRepoService::isValidUrl(const QString& url)
{
    if (url.isEmpty()) return false;
    if (!url.startsWith(QStringLiteral("http://"),  Qt::CaseInsensitive) &&
        !url.startsWith(QStringLiteral("https://"), Qt::CaseInsensitive)) {
        return false;
    }
    QUrl parsed(url);
    return parsed.isValid() && !parsed.host().isEmpty();
}

QList<mf::core::models::ExtensionManifest> ExtensionRepoService::parseManifestJson(
    const QByteArray& json)
{
    QList<mf::core::models::ExtensionManifest> result;

    QJsonParseError parseError;
    QJsonDocument doc = QJsonDocument::fromJson(json, &parseError);
    if (parseError.error != QJsonParseError::NoError) return result;
    if (!doc.isArray()) return result;

    const QJsonArray arr = doc.array();
    for (const QJsonValue& val : arr) {
        if (!val.isObject()) continue;
        const QJsonObject obj = val.toObject();

        mf::core::models::ExtensionManifest m;
        m.setId(obj.value(QStringLiteral("id")).toString());
        m.setName(obj.value(QStringLiteral("name")).toString());
        m.setVersion(obj.value(QStringLiteral("version")).toString());
        m.setAuthor(obj.value(QStringLiteral("author")).toString());
        m.setDescription(obj.value(QStringLiteral("description")).toString());
        m.setSourceType(obj.value(QStringLiteral("sourceType")).toString());
        m.setEntryPoint(obj.value(QStringLiteral("entryPoint")).toString());
        m.setFilePath(obj.value(QStringLiteral("filePath")).toString());

        if (m.id().isEmpty()) continue;

        if (obj.contains(QStringLiteral("configFields")) &&
            obj.value(QStringLiteral("configFields")).isArray()) {
            QList<mf::core::models::SourceConfigField> fields;
            const QJsonArray fieldsArr = obj.value(QStringLiteral("configFields")).toArray();
            for (const QJsonValue& fv : fieldsArr) {
                if (!fv.isObject()) continue;
                const QJsonObject fo = fv.toObject();
                mf::core::models::SourceConfigField f;
                f.setKey(fo.value(QStringLiteral("key")).toString());
                f.setLabel(fo.value(QStringLiteral("label")).toString());
                f.setPlaceholder(fo.value(QStringLiteral("placeholder")).toString());
                f.setDefaultValue(fo.value(QStringLiteral("defaultValue")).toString());
                f.setFieldType(fo.value(QStringLiteral("fieldType")).toString(
                    QStringLiteral("text")));
                f.setIsRequired(fo.value(QStringLiteral("required")).toBool(false));
                f.setIsPassword(fo.value(QStringLiteral("password")).toBool(false));

                if (fo.contains(QStringLiteral("options")) &&
                    fo.value(QStringLiteral("options")).isObject()) {
                    QHash<QString, QString> opts;
                    const QJsonObject optObj = fo.value(QStringLiteral("options")).toObject();
                    for (auto oit = optObj.constBegin(); oit != optObj.constEnd(); ++oit) {
                        opts.insert(oit.key(), oit.value().toString());
                    }
                    f.setOptions(std::move(opts));
                }

                fields.append(std::move(f));
            }
            m.setConfigFields(std::move(fields));
        }

        result.append(std::move(m));
    }

    return result;
}

// ── Query helpers ──────────────────────────────────────────────────

QList<mf::core::models::ExtensionManifest> ExtensionRepoService::manifestsForSourceType(
    const QString& sourceType) const
{
    QList<mf::core::models::ExtensionManifest> out;
    for (const mf::core::models::ExtensionManifest& m : manifests_) {
        if (m.sourceType().compare(sourceType, Qt::CaseInsensitive) == 0) {
            out.append(m);
        }
    }
    return out;
}

mf::core::models::ExtensionManifest ExtensionRepoService::manifestById(
    const QString& id) const
{
    for (const mf::core::models::ExtensionManifest& m : manifests_) {
        if (m.id().compare(id, Qt::CaseInsensitive) == 0) {
            return m;
        }
    }
    return mf::core::models::ExtensionManifest{};
}

} // namespace mf::core::services
