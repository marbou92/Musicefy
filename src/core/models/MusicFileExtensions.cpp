// MusicFileExtensions.cpp

#include "MusicFileExtensions.h"

namespace mf::core::models {

const QSet<QString>& MusicFileExtensions::All() {
    static const QSet<QString> set = {
        // Common
        QStringLiteral(".mp3"),
        QStringLiteral(".flac"),
        QStringLiteral(".wav"),
        QStringLiteral(".ogg"),
        QStringLiteral(".oga"),
        QStringLiteral(".m4a"),
        QStringLiteral(".aac"),
        QStringLiteral(".wma"),
        // Less common
        QStringLiteral(".ape"),
        QStringLiteral(".mpc"),
        QStringLiteral(".wv"),
        QStringLiteral(".aiff"),
        QStringLiteral(".aif"),
        // Hi-res
        QStringLiteral(".dsf"),
    };
    return set;
}

const QSet<QString>& MusicFileExtensions::Suffixes() {
    static const QSet<QString> set = {
        QStringLiteral("mp3"),
        QStringLiteral("flac"),
        QStringLiteral("wav"),
        QStringLiteral("ogg"),
        QStringLiteral("oga"),
        QStringLiteral("m4a"),
        QStringLiteral("aac"),
        QStringLiteral("wma"),
        QStringLiteral("ape"),
        QStringLiteral("mpc"),
        QStringLiteral("wv"),
        QStringLiteral("aiff"),
        QStringLiteral("aif"),
        QStringLiteral("dsf"),
    };
    return set;
}

const QStringList& MusicFileExtensions::SuffixList() {
    static const QStringList list = {
        QStringLiteral("*.mp3"),
        QStringLiteral("*.flac"),
        QStringLiteral("*.wav"),
        QStringLiteral("*.ogg"),
        QStringLiteral("*.oga"),
        QStringLiteral("*.m4a"),
        QStringLiteral("*.aac"),
        QStringLiteral("*.wma"),
        QStringLiteral("*.ape"),
        QStringLiteral("*.mpc"),
        QStringLiteral("*.wv"),
        QStringLiteral("*.aiff"),
        QStringLiteral("*.aif"),
        QStringLiteral("*.dsf"),
    };
    return list;
}

const QStringList& MusicFileExtensions::FolderArtNames() {
    static const QStringList list = {
        QStringLiteral("cover.jpg"),
        QStringLiteral("cover.png"),
        QStringLiteral("folder.jpg"),
        QStringLiteral("folder.png"),
        QStringLiteral("front.jpg"),
        QStringLiteral("front.png"),
        QStringLiteral("album.jpg"),
        QStringLiteral("album.png"),
        QStringLiteral("artwork.jpg"),
        QStringLiteral("artwork.png"),
        QStringLiteral("thumb.jpg"),
        QStringLiteral("thumb.png"),
    };
    return list;
}

} // namespace mf::core::models
