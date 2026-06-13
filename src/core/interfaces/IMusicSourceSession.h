#pragma once

#include "../models/MusicFile.h"

#include <QString>

#include <functional>

namespace mf::core::interfaces {

class IMusicSourceSession {
public:
    virtual ~IMusicSourceSession() = default;

    using ResultCallback = std::function<void(QList<mf::core::models::MusicFile>)>;
    using StringCallback = std::function<void(QString)>;
    using BytesCallback  = std::function<void(QByteArray)>;
    using BoolCallback   = std::function<void(bool, QString /*errorMessage*/)>;

    virtual QString sourceType() const = 0;
    virtual QString sourceId() const = 0;
    virtual bool    isHealthy() const = 0;

    virtual void searchTracks(QString query, int limit,
                              ResultCallback onDone,
                              StringCallback onError) = 0;
    virtual void fetchStreamUrl(QString trackId, StringCallback onDone, StringCallback onError) = 0;
    virtual void fetchLyrics(QString trackId, StringCallback onDone, StringCallback onError) = 0;
    virtual void fetchCover(QString trackId, BytesCallback onDone, StringCallback onError) = 0;

    virtual void ping(BoolCallback onDone) = 0;
};

} // namespace mf::core::interfaces
