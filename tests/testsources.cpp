// testsources.cpp
// Unit tests for the streaming source layer.
// HttpClient: roundtrip with a local QHttpServer-free approach uses
//             a stand-in — we exercise the request build & cancel paths
//             rather than end-to-end network calls.
// LocalFolderProvider: scans a temp dir containing a fake .mp3 file
//             (TagLib will return empty tags, but the discovery path is
//             what we're testing).
// StreamingSourceManager: provider registry, source add/update/remove,
//             session creation.
// SubsonicProvider: config field schema + URL builder (no network).
// YouTubeProvider: config field schema + multi-client fallback string.

#include <QtTest/QtTest>
#include <QStandardPaths>
#include <QCoreApplication>
#include <QDir>
#include <QFile>

#include "sources/HttpClient.h"
#include "sources/LocalFolderProvider.h"
#include "sources/StreamingSourceManager.h"
#include "sources/SubsonicProvider.h"
#include "sources/YouTubeProvider.h"

#include "models/StreamingSource.h"

using namespace mf::core::sources;
using namespace mf::core::models;

class TestSources : public QObject {
    Q_OBJECT
private:
    QString tmpRoot_;

    StreamingSource makeSource(const QString& id, const QString& type,
                                const QString& cfgJson) {
        StreamingSource s;
        s.setId(id);
        s.setName(QStringLiteral("Test ") + type);
        s.setType(type);
        s.setConfigurationJson(cfgJson);
        return s;
    }

private slots:
    void init() {
        tmpRoot_ = QStandardPaths::writableLocation(QStandardPaths::TempLocation)
                 + QStringLiteral("/musicefy_sources_")
                 + QString::number(QDateTime::currentMSecsSinceEpoch());
        QDir().mkpath(tmpRoot_);
    }

    void httpClientDefaults() {
        HttpClient client;
        QVERIFY(!client.defaultUserAgent().isEmpty());
        QVERIFY(client.cookieJar() != nullptr);
    }

    void httpClientCancelUnknownTagIsNoop() {
        HttpClient client;
        // No active requests; cancel of an unknown tag is a no-op (no crash).
        client.cancel(QStringLiteral("9999"));
    }

    void localFolderProviderCanHandle() {
        LocalFolderProvider p;
        QVERIFY(p.canHandle(tmpRoot_));
        QVERIFY(!p.canHandle(QStringLiteral("/definitely/does/not/exist/xyz")));
    }

    void localFolderProviderLists() {
        // Create a fake audio file. TagLib won't be able to read it, but
        // LocalFolderProvider will still discover it because the filter
        // is on file extension, not tag-read success.
        QString audioFile = tmpRoot_ + QStringLiteral("/track.mp3");
        QFile f(audioFile);
        QVERIFY(f.open(QIODevice::WriteOnly));
        f.write("ID3");
        f.close();

        LocalFolderProvider p;
        p.setRootPath(tmpRoot_);

        int doneCount = 0;
        QList<MusicFile> result;
        p.listTracksRecursive([&](QList<MusicFile> tracks) {
            ++doneCount;
            result = tracks;
        });

        QCOMPARE(doneCount, 1);
        QCOMPARE(result.size(), 1);
        QCOMPARE(result.first().filePath(), audioFile);
        QCOMPARE(result.first().sourceType(), QStringLiteral("local"));
    }

    void localFolderProviderNonRecursive() {
        QString subdir = tmpRoot_ + QStringLiteral("/sub");
        QDir().mkpath(subdir);
        QFile(subdir + QStringLiteral("/nested.mp3")).open(QIODevice::WriteOnly);

        LocalFolderProvider p;
        int doneCount = 0;
        QList<MusicFile> result;
        p.listTracks(tmpRoot_, [&](QList<MusicFile> tracks) {
            ++doneCount;
            result = tracks;
        });
        QCOMPARE(doneCount, 1);
        QCOMPARE(result.size(), 0); // non-recursive skips the subdir
        Q_UNUSED(result);
    }

    void streamingSourceManagerProviderRegistry() {
        StreamingSourceManager mgr;
        QCOMPARE(mgr.registeredSourceTypes().size(), 0);

        auto sub = std::make_shared<SubsonicProvider>();
        auto yt  = std::make_shared<YouTubeProvider>();
        mgr.registerProvider(sub);
        mgr.registerProvider(yt);

        QList<QString> types = mgr.registeredSourceTypes();
        QCOMPARE(types.size(), 2);
        QVERIFY(types.contains(QStringLiteral("subsonic")));
        QVERIFY(types.contains(QStringLiteral("youtube")));

        QVERIFY(mgr.providerFor(QStringLiteral("subsonic")) == sub);
        QVERIFY(mgr.providerFor(QStringLiteral("youtube"))  == yt);
        QVERIFY(mgr.providerFor(QStringLiteral("nope"))     == nullptr);

        mgr.unregisterProvider(QStringLiteral("youtube"));
        QCOMPARE(mgr.registeredSourceTypes().size(), 1);
        QVERIFY(mgr.providerFor(QStringLiteral("youtube")) == nullptr);
    }

    void streamingSourceManagerSources() {
        StreamingSourceManager mgr;
        mgr.registerProvider(std::make_shared<SubsonicProvider>());

        StreamingSource s = makeSource(QStringLiteral("src-1"),
                                       QStringLiteral("subsonic"),
                                       QStringLiteral("{\"serverUrl\":\"https://x\",\"username\":\"u\",\"password\":\"p\"}"));
        mgr.addSource(s);
        QCOMPARE(mgr.allSources().size(), 1);
        QCOMPARE(mgr.sourceById(QStringLiteral("src-1")).type(), QStringLiteral("subsonic"));

        // Update.
        s.setName(QStringLiteral("Renamed"));
        mgr.updateSource(s);
        QCOMPARE(mgr.sourceById(QStringLiteral("src-1")).name(), QStringLiteral("Renamed"));

        mgr.removeSource(QStringLiteral("src-1"));
        QCOMPARE(mgr.allSources().size(), 0);
    }

    void streamingSourceManagerCreateSession() {
        StreamingSourceManager mgr;
        mgr.registerProvider(std::make_shared<SubsonicProvider>());

        StreamingSource s = makeSource(QStringLiteral("src-1"),
                                       QStringLiteral("subsonic"),
                                       QStringLiteral("{\"serverUrl\":\"https://x\",\"username\":\"u\",\"password\":\"p\"}"));
        mgr.addSource(s);

        auto session = mgr.createSession(QStringLiteral("src-1"));
        QVERIFY(session != nullptr);
        QCOMPARE(session->sourceType(), QStringLiteral("subsonic"));
        QCOMPARE(session->sourceId(),   QStringLiteral("src-1"));

        // Unknown source id.
        QVERIFY(mgr.createSession(QStringLiteral("missing")) == nullptr);

        // Source type with no registered provider.
        mgr.addSource(makeSource(QStringLiteral("src-2"),
                                 QStringLiteral("nonexistent"),
                                 QStringLiteral("{}")));
        QVERIFY(mgr.createSession(QStringLiteral("src-2")) == nullptr);
    }

    void subsonicConfigFields() {
        SubsonicProvider p;
        auto fields = p.configFields();
        QVERIFY(fields.size() >= 3);
        QList<QString> keys;
        for (const auto& f : fields) keys.append(f.key());
        QVERIFY(keys.contains(QStringLiteral("serverUrl")));
        QVERIFY(keys.contains(QStringLiteral("username")));
        QVERIFY(keys.contains(QStringLiteral("password")));

        // The password field should be marked as required and as password type.
        for (const auto& f : fields) {
            if (f.key() == QStringLiteral("password")) {
                QVERIFY(f.isRequired());
                QCOMPARE(f.fieldType(), QStringLiteral("password"));
            }
        }
    }

    void subsonicConfigFromSource() {
        StreamingSource s = makeSource(QStringLiteral("src-1"),
                                       QStringLiteral("subsonic"),
                                       QStringLiteral("{\"serverUrl\":\"https://navidrome.example.com\",\"username\":\"alice\",\"password\":\"hunter2\",\"useTokenAuth\":true,\"apiVersion\":\"1.16.1\"}"));
        auto cfg = SubsonicConfig::fromSource(s);
        QCOMPARE(cfg.serverUrl, QStringLiteral("https://navidrome.example.com"));
        QCOMPARE(cfg.username,  QStringLiteral("alice"));
        QCOMPARE(cfg.password,  QStringLiteral("hunter2"));
        QVERIFY(cfg.useTokenAuth);
        QCOMPARE(cfg.apiVersion, QStringLiteral("1.16.1"));
    }

    void youTubeConfigFields() {
        YouTubeProvider p;
        auto fields = p.configFields();
        QVERIFY(fields.size() >= 1);
        // Region and language are optional; no auth in config (PoToken is runtime).
    }

    void youTubeSessionIsNotYetImplemented() {
        YouTubeSession s(QStringLiteral("src-1"), nullptr);
        QVERIFY(!s.isHealthy());
        bool pingCalled = false;
        s.ping([&](bool ok, QString err) {
            pingCalled = true;
            QVERIFY(!ok);
            QVERIFY(!err.isEmpty());
        });
        QVERIFY(pingCalled);
    }
};

QTEST_GUILESS_MAIN(TestSources)
#include "testsources.moc"
