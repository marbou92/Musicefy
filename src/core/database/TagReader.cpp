// TagReader.cpp
// Isolated TagLib wrapper. Kept in its own .cpp so the TagLib dependency
// is only linked in once, and so callers that don't need tag reading
// don't pay the include cost.

#include "LibraryScanner.h"
#include "../models/MusicFile.h"

#ifdef HAS_TAGLIB
#include <taglib/fileref.h>
#include <taglib/tag.h>
#endif

#include <QFileInfo>
#include <cmath>

namespace mf::core::database {

using mf::core::models::MusicFile;

MusicFile LibraryScanner::readTags(const QString& filePath) {
    MusicFile m;
    m.setFilePath(filePath);

#ifdef HAS_TAGLIB
    QByteArray utf8Path = filePath.toUtf8();
    TagLib::FileRef f(utf8Path.constData(), true);

    if (!f.isNull()) {
        TagLib::Tag* tag = f.tag();
        if (tag) {
            m.setTitle(QString::fromUtf8(tag->title().toCString(true)));
            m.setArtist(QString::fromUtf8(tag->artist().toCString(true)));
            m.setAlbum(QString::fromUtf8(tag->album().toCString(true)));
            m.setGenre(QString::fromUtf8(tag->genre().toCString(true)));
            m.setYear(static_cast<int>(tag->year()));
            m.setTrackNumber(static_cast<int>(tag->track()));
        }

        TagLib::AudioProperties* props = f.audioProperties();
        if (props) {
            int secs = props->length();
            m.setDuration(std::chrono::seconds{ std::max(0, secs) });
            m.setBitrate(props->bitrate());
        }
    }
#endif // HAS_TAGLIB

    QFileInfo fi(filePath);
    m.setFileSize(static_cast<qint64>(fi.size()));

    return m;
}

} // namespace mf::core::database
