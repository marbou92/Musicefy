#pragma once

#include "../models/MusicFile.h"

#include <QString>

#include <functional>

namespace mf::core::interfaces {

class IFolderDataProvider {
public:
    virtual ~IFolderDataProvider() = default;

    using TrackCallback = std::function<void(QList<mf::core::models::MusicFile>)>;

    virtual QString rootPath() const = 0;
    virtual void    setRootPath(QString path) = 0;

    virtual void listTracks(QString folderPath, TrackCallback onDone) = 0;
    virtual void listTracksRecursive(TrackCallback onDone) = 0;
    virtual bool canHandle(QString folderPath) const = 0;
};

} // namespace mf::core::interfaces
