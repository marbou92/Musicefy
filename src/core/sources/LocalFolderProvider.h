// LocalFolderProvider.h
// Walks a local folder and exposes the contained audio tracks as
// QList<MusicFile>. Tag reading uses LibraryScanner::readTags() (static),
// keeping the TagLib dependency isolated to the database layer.

#pragma once

#include "../interfaces/IFolderDataProvider.h"
#include "../models/MusicFile.h"

#include <QObject>
#include <QString>

namespace mf::core::sources {

class LocalFolderProvider : public QObject, public mf::core::interfaces::IFolderDataProvider {
    Q_OBJECT
public:
    explicit LocalFolderProvider(QObject* parent = nullptr);
    ~LocalFolderProvider() override;

    QString rootPath() const override { return rootPath_; }
    void    setRootPath(QString path) override { rootPath_ = std::move(path); }

    void listTracks(QString folderPath, TrackCallback onDone) override;
    void listTracksRecursive(TrackCallback onDone) override;
    bool canHandle(QString folderPath) const override;

signals:
    void scanProgress(int processed, const QString& currentFile);
    void scanFinished(int totalTracks);

private:
    void listUnder(const QString& folder, bool recursive, TrackCallback onDone);

    QString rootPath_;
};

} // namespace mf::core::sources
