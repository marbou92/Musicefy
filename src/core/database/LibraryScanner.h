#pragma once

#include "../models/MusicFile.h"

#include <QList>
#include <QString>
#include <QStringList>
#include <optional>

#include <functional>

namespace mf::core::database {

class Database;

struct ScanProgress {
    int current = 0;
    int total   = 0;
    QString currentFile;
};

class LibraryScanner {
public:
    using ProgressCallback = std::function<void(ScanProgress)>;

    explicit LibraryScanner(Database& db);

    /// Walks a list of root folders, extracts tags, upserts into the DB.
    void scan(const QStringList& rootFolders, ProgressCallback onProgress = nullptr);

    /// Process a single file.
    bool processFile(const QString& filePath);

    void cancel();
    bool isCancelled() const { return cancelled_; }

    int totalProcessed() const { return totalProcessed_; }
    int totalAdded() const { return totalAdded_; }
    int totalUpdated() const { return totalUpdated_; }

    /// Returns true if the file extension is one we know how to read tags for.
    static bool isSupportedFile(const QString& filePath);

    /// Read tags via TagLib. The implementation lives in a separate .cpp to
    /// keep the TagLib dependency isolated.
    static mf::core::models::MusicFile readTags(const QString& filePath);

private:
    void walkFolder(const QString& folder, const QStringList& audioExtensions, ProgressCallback onProgress);

    Database& db_;
    bool cancelled_ = false;
    int totalProcessed_ = 0;
    int totalAdded_ = 0;
    int totalUpdated_ = 0;
};

} // namespace mf::core::database
