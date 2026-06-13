// testmodels.cpp
// Smoke tests for the model layer.

#include "MusicFile.h"
#include "ArtistInfo.h"
#include "AlbumInfo.h"
#include "PlaylistInfo.h"
#include "StreamingSource.h"
#include "SourceConfigField.h"
#include "SourceHealthState.h"
#include "SearchHistory.h"
#include "SearchResultGroup.h"
#include "SearchSourceMode.h"
#include "BrowseSection.h"
#include "HomeSection.h"
#include "ExtensionManifest.h"
#include "AutoPlaylistInfo.h"
#include "SourceTypes.h"
#include "MusicFileExtensions.h"

#include <QTest>

using namespace mf::core::models;

class TestModels : public QObject {
    Q_OBJECT

private slots:
    void musicFileEqualityByPath();
    void musicFileMarkPlayed();
    void sourceTypesAreStrings();
    void extensionsAreNonEmpty();
    void sourceHealthStateExponentialBackoff();
    void searchHistoryClickCount();
    void extensionManifestStoresConfigFields();
    void streamingSourceConfiguration();
    void autoPlaylistRefreshDefaultReturnsCurrent();
};

void TestModels::musicFileEqualityByPath() {
    MusicFile a;
    a.setFilePath("C:/Music/track.mp3");
    MusicFile b;
    b.setFilePath("c:/music/TRACK.mp3");
    QVERIFY(a == b);
}

void TestModels::musicFileMarkPlayed() {
    MusicFile m;
    m.setFilePath("C:/Music/track.mp3");
    QCOMPARE(m.playCount(), 0);
    m.markPlayed();
    m.markPlayed();
    QCOMPARE(m.playCount(), 2);
    QVERIFY(m.lastPlayed().isValid());
}

void TestModels::sourceTypesAreStrings() {
    QVERIFY(!QString::fromLatin1(mf::core::models::kSourceTypeLocal).isEmpty());
    QVERIFY(!QString::fromLatin1(mf::core::models::kSourceTypeYouTube).isEmpty());
    QVERIFY(!QString::fromLatin1(mf::core::models::kSourceTypeSubsonic).isEmpty());
    QVERIFY(!QString::fromLatin1(mf::core::models::kSourceTypeExtension).isEmpty());
}

void TestModels::extensionsAreNonEmpty() {
    QVERIFY(mf::core::models::MusicFileExtensions::SuffixList().size() > 0);
    QVERIFY(mf::core::models::kFolderArtFilenames.size() > 0);
}

void TestModels::sourceHealthStateExponentialBackoff() {
    SourceHealthState h;
    h.setSourceId("test");
    QCOMPARE(h.status(), SourceHealthStatus::Healthy);
    h.recordFailure("err1");
    QCOMPARE(h.status(), SourceHealthStatus::Degraded);
    h.recordFailure("err2");
    QCOMPARE(h.status(), SourceHealthStatus::Degraded);
    h.recordFailure("err3");
    QCOMPARE(h.status(), SourceHealthStatus::Unhealthy);
    h.recordFailure("err4");
    QCOMPARE(h.status(), SourceHealthStatus::Unhealthy);
    h.recordFailure("err5");
    QCOMPARE(h.status(), SourceHealthStatus::PermanentlyUnhealthy);
    h.recordSuccess();
    QCOMPARE(h.status(), SourceHealthStatus::Healthy);
    QCOMPARE(h.consecutiveFailures(), 0);
}

void TestModels::searchHistoryClickCount() {
    SearchHistory h;
    h.setQuery("hello");
    h.markClicked();
    h.markClicked();
    QCOMPARE(h.clickCount(), 2);
}

void TestModels::extensionManifestStoresConfigFields() {
    ExtensionManifest m;
    m.setId("ext1");
    m.setName("TestExtension");
    SourceConfigField f;
    f.setKey("apiKey");
    f.setLabel("API Key");
    f.setIsPassword(true);
    m.setConfigFields({f});
    QCOMPARE(m.configFields().size(), 1);
    QCOMPARE(m.configFields().first().key(), QStringLiteral("apiKey"));
    QVERIFY(m.configFields().first().isPassword());
}

void TestModels::streamingSourceConfiguration() {
    StreamingSource s;
    s.setId("sub1");
    s.setName("My Subsonic");
    s.setType(QStringLiteral("Subsonic"));
    s.setUrl(QStringLiteral("https://navidrome.example.com"));
    s.setUsername(QStringLiteral("alice"));
    s.setPassword(QStringLiteral("hunter2"));
    s.ensureConfiguration();
    QVERIFY(s.configuration().contains(QStringLiteral("url")));
    QVERIFY(s.configuration().contains(QStringLiteral("username")));
    QVERIFY(!s.configuration().contains(QStringLiteral("password")));
}

void TestModels::autoPlaylistRefreshDefaultReturnsCurrent() {
    AutoPlaylistInfo p;
    QVERIFY(!p.hasRefreshFn());
    MusicFile m;
    m.setFilePath("C:/x.mp3");
    p.setCurrentTracks({m});
    QCOMPARE(p.refresh().size(), 1);
}

QTEST_GUILESS_MAIN(TestModels)
#include "testmodels.moc"
