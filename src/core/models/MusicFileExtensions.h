// MusicFileExtensions.h
// Set of valid audio file extensions. Port of Musicefy.Core.Models.MusicFileExtensions.

#pragma once

#include <QSet>
#include <QString>
#include <QStringList>

namespace mf::core::models {

class MusicFileExtensions {
public:
    /// Returns the set of audio file extensions (lowercase, with leading dot)
    /// that the library scanner recognizes.
    static const QSet<QString>& All();

    /// Returns the same set as suffixes without the leading dot
    /// (e.g. "mp3", "flac"). Convenient for QFileInfo::suffix() matching.
    static const QSet<QString>& Suffixes();

    /// Convenience QStringList of suffixes — what QDirIterator wants.
    static const QStringList& SuffixList();

    /// Returns the priority list of folder artwork filenames used when
    /// no embedded picture is found in a track's tags.
    static const QStringList& FolderArtNames();
};

} // namespace mf::core::models


