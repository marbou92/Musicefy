// MediaKeyFilter.cpp
// Windows implementation uses WM_APPCOMMAND. The actual filter is a
// QAbstractNativeEventFilter declared privately and stored via a pimpl
// pattern to keep <windows.h> out of the public header.

#include "MediaKeyFilter.h"

#include <QAbstractNativeEventFilter>
#include <QCoreApplication>

#ifdef Q_OS_WIN
#  include <windows.h>
#endif

namespace mf::core::playback {

struct MediaKeyFilter::FilterImpl : public QAbstractNativeEventFilter {
    MediaKeyFilter* owner = nullptr;

    explicit FilterImpl(MediaKeyFilter* o) : owner(o) {}

#if defined(Q_OS_WIN)
    bool nativeEventFilter(const QByteArray& eventType, void* message, long* result) override {
        Q_UNUSED(result);
        if (eventType != "windows_generic_MSG" && eventType != "windows_dispatcher_MSG") {
            return false;
        }
        auto* msg = static_cast<MSG*>(message);
        if (msg->message != WM_APPCOMMAND) {
            return false;
        }
        // The APPCOMMAND is packed in the low 12 bits of lParam.
        // Reference: https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-appcommand
        int cmd = (msg->lParam & 0xFFF);
        // The high bit is the "from" device; we don't care.
        MediaKey key = MediaKey::PlayPause;
        bool known = true;
        switch (cmd) {
            case APPCOMMAND_MEDIA_PLAY_PAUSE: key = MediaKey::PlayPause;   break;
            case APPCOMMAND_MEDIA_NEXTTRACK:  key = MediaKey::Next;        break;
            case APPCOMMAND_MEDIA_PREVIOUSTRACK: key = MediaKey::Previous; break;
            case APPCOMMAND_MEDIA_STOP:       key = MediaKey::Stop;        break;
            case APPCOMMAND_VOLUME_UP:        key = MediaKey::VolumeUp;    break;
            case APPCOMMAND_VOLUME_DOWN:      key = MediaKey::VolumeDown;  break;
            case APPCOMMAND_VOLUME_MUTE:      key = MediaKey::VolumeMute;  break;
            default: known = false; break;
        }
        if (known) {
            QMetaObject::invokeMethod(owner, "mediaKeyPressed",
                                      Qt::QueuedConnection,
                                      Q_ARG(int, int(key)));
            return true; // We handled it; stop further propagation.
        }
        return false;
    }
#else
    bool nativeEventFilter(const QByteArray& eventType, void* message, long* result) override {
        Q_UNUSED(eventType);
        Q_UNUSED(message);
        Q_UNUSED(result);
        return false;
    }
#endif
};

MediaKeyFilter::MediaKeyFilter(QObject* parent)
    : QObject(parent)
    , impl_(new FilterImpl(this))
{
    if (auto* app = QCoreApplication::instance()) {
        app->installNativeEventFilter(impl_);
    }
}

MediaKeyFilter::~MediaKeyFilter() {
    if (auto* app = QCoreApplication::instance()) {
        app->removeNativeEventFilter(impl_);
    }
    delete impl_;
}

} // namespace mf::core::playback