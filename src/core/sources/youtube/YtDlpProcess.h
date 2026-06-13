// YtDlpProcess.h
// Asynchronous wrapper around the `yt-dlp` command-line tool, used as
// a last-resort fallback for YouTube endpoints when the InnerTube
// path fails (cipher changes, JS not parseable, full bot block).
//
// yt-dlp ships as a single executable (a release binary) and exposes
// everything we need via the CLI:
//   * `--get-url -f bestaudio`   → returns a direct stream URL
//   * `--dump-json --flat-playlist` `ytsearch25:<q>`  → search results
//   * `--version`                → sanity check / version pin
//
// The wrapper uses QProcess (async, signal-driven). The
// YouTubeProvider calls into this only when its InnerTube + cipher
// paths have both failed, so the user-visible cost is one extra
// process spawn (~100-300 ms) per "this track wouldn't play" case.
//
// Executable resolution order:
//   1. explicit path passed in (test seam + per-user override)
//   2. SettingsControl key `youtube/ytDlpPath` (not consulted here;
//      callers should resolve and pass in)
//   3. `app/localdata/tools/yt-dlp.exe` next to the app binary
//   4. `yt-dlp.exe` / `yt-dlp` in PATH
//   5. `python -m yt_dlp` (works on dev machines with a system Python)

#pragma once

#include <QObject>
#include <QPointer>
#include <QString>
#include <QStringList>

#include <functional>
#include <memory>

class QProcess;

namespace mf::core::sources::youtube {

class YtDlpProcess : public QObject {
    Q_OBJECT
public:
    explicit YtDlpProcess(QObject* parent = nullptr);
    ~YtDlpProcess() override;

    // Try to find a working yt-dlp executable on this machine.
    // Returns an empty string if nothing was found. The optional
    // `hint` is prepended to the resolution chain (the explicit
    // override path).
    static QString findExecutable(const QString& hint = QString());

    // Returns true if `findExecutable()` returns a non-empty string.
    // Convenience for the health check / provider config.
    static bool isAvailable(const QString& hint = QString());

    // Pins the executable used for all subsequent calls. Pass an
    // empty string to fall back to auto-detection.
    void setExecutable(QString path);
    QString executable() const { return executable_; }

    // The arguments appended to the base yt-dlp command for each
    // public method. Exposed for tests so they can assert that the
    // exact CLI was issued.
    QStringList lastArgs() const { return lastArgs_; }
    QString     lastError() const { return lastError_; }

    // Async: runs `yt-dlp --get-url -f bestaudio <url>` and returns
    // the first non-empty line of stdout (the direct stream URL).
    using StringCallback = std::function<void(QString /*result*/,
                                              QString /*error*/)>;
    void resolveStreamUrl(const QString& videoUrl, StringCallback cb);

    // Async: runs `yt-dlp --dump-json --flat-playlist
    // "ytsearch25:<query>"` and returns each entry's `id`, `title`,
    // `uploader`, and `duration` as a flat list. Empty list on
    // failure (with `error` populated).
    struct SearchEntry {
        QString id;
        QString title;
        QString uploader;
        qint64  durationSeconds = 0;
    };
    using SearchCallback = std::function<void(QList<SearchEntry> /*result*/,
                                              QString /*error*/)>;
    void search(QString query, int limit, SearchCallback cb);

    // Async: runs `yt-dlp --version`. Used by the settings panel to
    // show "yt-dlp 2025.09.26" in the diagnostics.
    using VersionCallback = std::function<void(QString /*version*/,
                                               QString /*error*/)>;
    void getVersion(VersionCallback cb);

    // Test seam: when `active` is true, the three async methods
    // short-circuit and call their callbacks synchronously with the
    // canned data, bypassing the QProcess spawn. Production code
    // leaves this alone.
    struct FakeOutput {
        bool     active = false;
        QString  streamUrl;
        QString  error;
        QList<SearchEntry> searchResults;
        QString  version;
    };
    void setFakeOutput(FakeOutput out) { fakeOutput_ = std::move(out); }
    bool hasFakeOutput() const { return fakeOutput_.active; }

private:
    void startProcess(const QStringList& args, StringCallback cb);
    void startProcessMulti(const QStringList& args, SearchCallback cb);
    void startProcessVersion(const QStringList& args, VersionCallback cb);

    QPointer<QProcess> process_;
    QString            executable_;
    QStringList        lastArgs_;
    QString            lastError_;
    FakeOutput         fakeOutput_;
};

} // namespace mf::core::sources::youtube
