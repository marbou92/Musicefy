// testsourceviewmodel.cpp
// Unit tests for SourceViewModel: property accessors, expand toggle,
// and health check flow via a mock IStreamingSourceManager.

#include <QtTest/QtTest>
#include <QSignalSpy>
#include <QTimer>

#include "viewmodels/SourceViewModel.h"
#include "core/interfaces/IStreamingSourceManager.h"
#include "core/interfaces/IMusicSourceSession.h"
#include "core/models/StreamingSource.h"

using mf::app::viewmodels::SourceViewModel;
using mf::core::interfaces::IStreamingSourceManager;
using mf::core::interfaces::IMusicSourceSession;
using mf::core::models::StreamingSource;
using mf::core::models::SourceHealthStatus;

// ── Mock session ─────────────────────────────────────────────────
class MockSession : public IMusicSourceSession {
public:
    explicit MockSession(bool healthy = true, QString error = {})
        : healthy_(healthy), error_(std::move(error)) {}

    QString sourceType() const override { return QStringLiteral("Test"); }
    QString sourceId()   const override { return QStringLiteral("mock-1"); }
    bool    isHealthy()  const override { return healthy_; }

    void searchTracks(QString, int, ResultCallback, StringCallback) override {}
    void fetchStreamUrl(QString, StringCallback, StringCallback) override {}
    void fetchLyrics(QString, StringCallback, StringCallback) override {}
    void fetchCover(QString, BytesCallback, StringCallback) override {}

    void ping(BoolCallback onDone) override {
        if (onDone) onDone(healthy_, error_);
    }

private:
    bool healthy_;
    QString error_;
};

// ── Mock source manager ──────────────────────────────────────────
class MockSourceManager : public IStreamingSourceManager {
public:
    void registerProvider(std::shared_ptr<IMusicSourceProvider>) override {}
    void unregisterProvider(QString) override {}
    QList<QString> registeredSourceTypes() const override { return {}; }
    std::shared_ptr<IMusicSourceProvider> providerFor(QString) const override { return nullptr; }

    void addSource(StreamingSource) override {}
    void updateSource(StreamingSource) override {}
    void removeSource(QString) override {}
    QList<StreamingSource> allSources() const override { return {}; }
    StreamingSource sourceById(QString) const override { return {}; }

    std::unique_ptr<IMusicSourceSession> createSession(QString) override {
        return std::make_unique<MockSession>(healthy_, error_);
    }

    void setPingHealthy(bool v) { healthy_ = v; }
    void setPingError(QString e) { error_ = std::move(e); }

private:
    bool healthy_ = true;
    QString error_;
};

// ── Tests ────────────────────────────────────────────────────────
class TestSourceViewModel : public QObject {
    Q_OBJECT

private:
    MockSourceManager mgr_;

private slots:
    void id_returnsSourceId() {
        StreamingSource src;
        src.setId(QStringLiteral("src-1"));
        src.setName(QStringLiteral("My Subsonic"));
        SourceViewModel vm(&mgr_, src);
        QCOMPARE(vm.id(), QStringLiteral("src-1"));
    }

    void name_returnsSourceName() {
        StreamingSource src;
        src.setName(QStringLiteral("My Subsonic"));
        SourceViewModel vm(&mgr_, src);
        QCOMPARE(vm.name(), QStringLiteral("My Subsonic"));
    }

    void type_returnsSourceType() {
        StreamingSource src;
        src.setType(QStringLiteral("Subsonic"));
        SourceViewModel vm(&mgr_, src);
        QCOMPARE(vm.type(), QStringLiteral("Subsonic"));
    }

    void url_returnsSourceUrl() {
        StreamingSource src;
        src.setUrl(QStringLiteral("https://navidrome.example.com"));
        SourceViewModel vm(&mgr_, src);
        QCOMPARE(vm.url(), QStringLiteral("https://navidrome.example.com"));
    }

    void isConnected_disconnectedByDefault() {
        StreamingSource src;
        src.setIsConnected(false);
        SourceViewModel vm(&mgr_, src);
        QVERIFY(!vm.isConnected());
    }

    void isConnected_connected() {
        StreamingSource src;
        src.setIsConnected(true);
        SourceViewModel vm(&mgr_, src);
        QVERIFY(vm.isConnected());
    }

    void isExpanded_defaultFalse() {
        StreamingSource src;
        SourceViewModel vm(&mgr_, src);
        QVERIFY(!vm.isExpanded());
    }

    void setExpanded_true_emitsSignal() {
        StreamingSource src;
        SourceViewModel vm(&mgr_, src);
        QSignalSpy spy(&vm, &SourceViewModel::expandedChanged);
        vm.setExpanded(true);
        QCOMPARE(spy.count(), 1);
        QVERIFY(vm.isExpanded());
    }

    void setExpanded_sameValue_noEmit() {
        StreamingSource src;
        SourceViewModel vm(&mgr_, src);
        vm.setExpanded(false);
        QSignalSpy spy(&vm, &SourceViewModel::expandedChanged);
        vm.setExpanded(false);
        QCOMPARE(spy.count(), 0);
    }

    void setExpanded_toggleBack() {
        StreamingSource src;
        SourceViewModel vm(&mgr_, src);
        vm.setExpanded(true);
        QVERIFY(vm.isExpanded());
        vm.setExpanded(false);
        QVERIFY(!vm.isExpanded());
    }

    void healthState_healthyWhenConnected() {
        StreamingSource src;
        src.setIsConnected(true);
        SourceViewModel vm(&mgr_, src);
        // HealthState is SourceHealthStatus::Healthy (enum value 0)
        QCOMPARE(vm.healthState(), static_cast<int>(SourceHealthStatus::Healthy));
    }

    void healthStatusText_connected() {
        StreamingSource src;
        src.setIsConnected(true);
        SourceViewModel vm(&mgr_, src);
        QCOMPARE(vm.healthStatusText(), QStringLiteral("Connected"));
    }

    void healthStatusText_disconnected() {
        StreamingSource src;
        src.setIsConnected(false);
        SourceViewModel vm(&mgr_, src);
        QCOMPARE(vm.healthStatusText(), QStringLiteral("Not connected"));
    }

    void errorMessage_emptyByDefault() {
        StreamingSource src;
        SourceViewModel vm(&mgr_, src);
        QVERIFY(vm.errorMessage().isEmpty());
    }

    void checkHealth_success_updatesHealth() {
        mgr_.setPingHealthy(true);
        StreamingSource src;
        src.setId(QStringLiteral("s1"));
        SourceViewModel vm(&mgr_, src);
        vm.checkHealth();
        // Ping callback is synchronous in the mock
        QCOMPARE(vm.healthState(), static_cast<int>(SourceHealthStatus::Healthy));
        QCOMPARE(vm.healthStatusText(), QStringLiteral("Connected"));
        QVERIFY(vm.errorMessage().isEmpty());
    }

    void checkHealth_failure_setsError() {
        mgr_.setPingHealthy(false);
        mgr_.setPingError(QStringLiteral("Connection refused"));
        StreamingSource src;
        src.setId(QStringLiteral("s2"));
        SourceViewModel vm(&mgr_, src);
        vm.checkHealth();
        QCOMPARE(vm.healthState(), static_cast<int>(SourceHealthStatus::Unhealthy));
        QCOMPARE(vm.healthStatusText(), QStringLiteral("Error"));
        QCOMPARE(vm.errorMessage(), QStringLiteral("Connection refused"));
    }

    void checkHealth_setsLastHealthCheck() {
        mgr_.setPingHealthy(true);
        StreamingSource src;
        SourceViewModel vm(&mgr_, src);
        QVERIFY(vm.lastHealthCheck().isEmpty());
        vm.checkHealth();
        QVERIFY(!vm.lastHealthCheck().isEmpty());
    }

    void checkHealth_emitsHealthChanged() {
        mgr_.setPingHealthy(true);
        StreamingSource src;
        SourceViewModel vm(&mgr_, src);
        QSignalSpy spy(&vm, &SourceViewModel::healthChanged);
        vm.checkHealth();
        QVERIFY(spy.count() >= 1);
    }
};

QTEST_MAIN(TestSourceViewModel)
#include "testsourceviewmodel.moc"
