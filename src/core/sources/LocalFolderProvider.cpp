// LocalFolderProvider.cpp
// See header for design notes.

#include "LocalFolderProvider.h"

#include "../database/LibraryScanner.h"
#include "../models/MusicFileExtensions.h"

#include <QDir>
#include <QDirIterator>
#include <QFileInfo>

namespace mf::core::sources {

using mf::core::database::LibraryScanner;
using mf::core::interfaces::IFolderDataProvider;
using mf::core::models::MusicFile;
using mf::core::models::MusicFileExtensions;

LocalFolderProvider::LocalFolderProvider(QObject* parent)
    : QObject(parent)
{
}

LocalFolderProvider::~LocalFolderProvider() = default;

bool LocalFolderProvider::canHandle(QString folderPath) const {
    QFileInfo fi(folderPath);
    return fi.exists() && fi.isDir();
}

void LocalFolderProvider::listTracks(QString folderPath, TrackCallback onDone) {
    listUnder(folderPath, /*recursive=*/false, std::move(onDone));
}

void LocalFolderProvider::listTracksRecursive(TrackCallback onDone) {
    listUnder(rootPath_, /*recursive=*/true, std::move(onDone));
}

void LocalFolderProvider::listUnder(const QString& folder, bool recursive, TrackCallback onDone) {
    QDir d(folder);
    if (!d.exists()) {
        if (onDone) onDone({});
        return;
    }

    QDirIterator::IteratorFlags flags;
    if (recursive) flags |= QDirIterator::Subdirectories;

    QList<MusicFile> out;
    int processed = 0;
    QDirIterator it(folder, MusicFileExtensions::SuffixList(), QDir::Files, flags);
    while (it.hasNext()) {
        QString path = it.next();
        MusicFile m = LibraryScanner::readTags(path);
        m.setFilePath(path);
        m.setSourceType(QStringLiteral("local"));
        out.append(std::move(m));
        ++processed;
        emit scanProgress(processed, path);
    }

    emit scanFinished(out.size());
    if (onDone) onDone(out);
}

} // namespace mf::core::sources