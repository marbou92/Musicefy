#pragma once

#include "../models/MusicFile.h"

#include <QString>

#include <functional>

namespace mf::core::interfaces {

class IDownloadService {
public:
    virtual ~IDownloadService() = default;

    using ProgressCallback = std::function<void(QString trackId, int percent)>;
    using CompletionCallback = std::function<void(QString trackId, bool ok, QString localPath, QString errorMessage)>;

    virtual void download(mf::core::models::MusicFile track, CompletionCallback onDone) = 0;
    virtual void cancel(QString trackId) = 0;
    virtual bool isDownloading(QString trackId) const = 0;
    virtual bool isDownloaded(QString trackId) const = 0;
    virtual QString localPathFor(QString trackId) const = 0;
    virtual void removeDownload(QString trackId) = 0;

    virtual void setOnProgress(ProgressCallback cb) = 0;
    virtual void setOnCompletion(CompletionCallback cb) = 0;
};

} // namespace mf::core::interfaces
