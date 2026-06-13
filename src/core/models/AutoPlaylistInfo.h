#pragma once

#include "MusicFile.h"

#include <QString>

#include <functional>

namespace mf::core::models {

class AutoPlaylistInfo {
public:
    /// Type-safe rule enum for auto-playlists.
    enum class RuleType {
        MostPlayed,
        RecentlyPlayed,
        RecentlyAdded,
        TopRated,
        Favorites
    };

    static QString ruleTypeToString(RuleType t) {
        switch (t) {
        case RuleType::MostPlayed:      return QStringLiteral("MostPlayed");
        case RuleType::RecentlyPlayed:  return QStringLiteral("RecentlyPlayed");
        case RuleType::RecentlyAdded:   return QStringLiteral("RecentlyAdded");
        case RuleType::TopRated:        return QStringLiteral("TopRated");
        case RuleType::Favorites:       return QStringLiteral("Favorites");
        }
        return QStringLiteral("MostPlayed");
    }

    static RuleType ruleTypeFromString(const QString& s) {
        if (s == QStringLiteral("RecentlyPlayed"))  return RuleType::RecentlyPlayed;
        if (s == QStringLiteral("RecentlyAdded"))   return RuleType::RecentlyAdded;
        if (s == QStringLiteral("TopRated"))        return RuleType::TopRated;
        if (s == QStringLiteral("Favorites"))       return RuleType::Favorites;
        return RuleType::MostPlayed;
    }

    QString id() const { return id_; }
    void setId(QString v) { id_ = std::move(v); }

    QString name() const { return name_; }
    void setName(QString v) { name_ = std::move(v); }

    QString description() const { return description_; }
    void setDescription(QString v) { description_ = std::move(v); }

    QString coverPath() const { return coverPath_; }
    void setCoverPath(QString v) { coverPath_ = std::move(v); }

    QString sourceType() const { return sourceType_; }
    void setSourceType(QString v) { sourceType_ = std::move(v); }

    /// Type-safe rule type accessor.
    RuleType ruleType() const { return ruleType_; }
    void setRuleType(RuleType v) { ruleType_ = v; }

    /// Convenience: set rule type from a persisted string.
    void setRuleTypeFromString(const QString& v) { ruleType_ = ruleTypeFromString(v); }

    /// Convenience: get the rule type as a persisted string.
    QString ruleTypeString() const { return ruleTypeToString(ruleType_); }

    int limit() const { return limit_; }
    void setLimit(int v) { limit_ = v; }

    QList<MusicFile> currentTracks() const { return currentTracks_; }
    void setCurrentTracks(QList<MusicFile> v) { currentTracks_ = std::move(v); }

    /// Optional refresh callback. The producer of the auto-playlist owns
    /// this and is expected to provide an implementation that re-runs the
    /// rule query against the library.
    using RefreshFn = std::function<QList<MusicFile>(const AutoPlaylistInfo&)>;
    void setRefreshFn(RefreshFn fn) { refreshFn_ = std::move(fn); }
    bool hasRefreshFn() const { return static_cast<bool>(refreshFn_); }
    QList<MusicFile> refresh() const;

private:
    QString id_;
    QString name_;
    QString description_;
    QString coverPath_;
    QString sourceType_;
    RuleType ruleType_ = RuleType::MostPlayed;
    int limit_ = 50;
    QList<MusicFile> currentTracks_;
    RefreshFn refreshFn_;
};

} // namespace mf::core::models
