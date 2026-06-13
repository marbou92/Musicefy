// MediaKeyFilter.h
// Listens for global media keys (Play/Pause, Next, Previous, Stop, Volume
// Up/Down/Mute) on Windows. The Qt main event loop is augmented with a
// native event filter that intercepts WM_APPCOMMAND messages.
//
// On non-Windows platforms the filter is a no-op so the build still works
// (the rest of the app is portable). Wire it into the QApplication with
// QCoreApplication::installNativeEventFilter() in main().
//
// Reference: https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-appcommand

#pragma once

#include <QObject>

#include <array>

class QAbstractNativeEventFilter;

namespace mf::core::playback {

enum class MediaKey {
    PlayPause,
    Next,
    Previous,
    Stop,
    VolumeUp,
    VolumeDown,
    VolumeMute,
};

class MediaKeyFilter : public QObject {
    Q_OBJECT
public:
    explicit MediaKeyFilter(QObject* parent = nullptr);
    ~MediaKeyFilter() override;

    // QAbstractNativeEventFilter is implemented on the *FilterImpl* class
    // defined in the .cpp. The QObject derives from it to expose signals.
    // We don't need a public install() — caller does
    //   QGuiApplication::instance()->installNativeEventFilter(filter);
    //   ...or...
    //   QCoreApplication::instance()->installNativeEventFilter(filter);

signals:
    void mediaKeyPressed(int key); // MediaKey as int

private:
    struct FilterImpl;
    FilterImpl* impl_ = nullptr;
};

} // namespace mf::core::playback
