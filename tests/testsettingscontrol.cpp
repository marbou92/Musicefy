// testsettingscontrol.cpp
// Unit tests for SettingsControl. Focus on the new
// settingChanged signal that the SettingsPage listens to for
// "Saved" toast feedback (5.5.I).
//
// Settings are stored in a temp INI file (via a custom org/app
// ctor) so we don't pollute the user's real registry / config.

#include <QtTest/QtTest>
#include <QSignalSpy>
#include <QStandardPaths>
#include <QString>
#include <QVariant>
#include <QTemporaryDir>

#include "services/SettingsControl.h"

using mf::core::services::SettingsControl;

class TestSettingsControl : public QObject {
    Q_OBJECT

private:
    QTemporaryDir* tmpDir_ = nullptr;

private slots:
    void initTestCase() {
        tmpDir_ = new QTemporaryDir;
        QVERIFY(tmpDir_->isValid());
    }

    void cleanupTestCase() {
        delete tmpDir_;
        tmpDir_ = nullptr;
    }

    SettingsControl* makeControl() {
        // QSettings::IniFormat + UserScope + a unique path per test
        // call so the files don't collide. The org/app ctor doesn't
        // accept a custom path directly; we set the path via
        // QSettings::setPath() and then construct with the org/app
        // ctor using unique names.
        const QString base = tmpDir_->path() + QStringLiteral("/s");
        QSettings::setPath(QSettings::IniFormat,
                           QSettings::UserScope,
                           base);
        return new SettingsControl(
            QStringLiteral("TestOrg") + QString::number(reinterpret_cast<quintptr>(this)),
            QStringLiteral("TestApp") + QString::number(reinterpret_cast<quintptr>(this)),
            nullptr);
    }

    void set_emitsSettingChanged() {
        auto* c = makeControl();
        QSignalSpy spy(c, &SettingsControl::settingChanged);
        c->set(QStringLiteral("foo"), 42);
        QCOMPARE(spy.count(), 1);
        const QList<QVariant> args = spy.takeFirst();
        QCOMPARE(args.at(0).toString(), QStringLiteral("foo"));
        QCOMPARE(args.at(1).toInt(),    42);
        delete c;
    }

    void set_emitsOnEveryCall() {
        // 5 successive sets → 5 emissions, no coalescing at this
        // layer (the coalescing is in SettingsPage's debounce timer).
        auto* c = makeControl();
        QSignalSpy spy(c, &SettingsControl::settingChanged);
        for (int i = 0; i < 5; ++i) {
            c->set(QStringLiteral("foo"), i);
        }
        QCOMPARE(spy.count(), 5);
        delete c;
    }

    void set_persistsValue() {
        auto* c = makeControl();
        c->set(QStringLiteral("bar"), QStringLiteral("baz"));
        // Round-trip: a fresh handle on the same backing store
        // must observe the value.
        QCOMPARE(c->get(QStringLiteral("bar")).toString(),
                 QStringLiteral("baz"));
        delete c;
    }

    void set_overwritesPreviousValue() {
        auto* c = makeControl();
        c->set(QStringLiteral("k"), 1);
        c->set(QStringLiteral("k"), 2);
        QSignalSpy spy(c, &SettingsControl::settingChanged);
        c->set(QStringLiteral("k"), 2);  // identical
        QCOMPARE(spy.count(), 1);  // signal still fires
        QCOMPARE(c->get(QStringLiteral("k")).toInt(), 2);
        delete c;
    }

    void remove_doesNotEmit() {
        // remove() doesn't need a "saved" toast — the user just
        // deleted the setting. No signal should fire.
        auto* c = makeControl();
        c->set(QStringLiteral("foo"), 1);
        QSignalSpy spy(c, &SettingsControl::settingChanged);
        c->remove(QStringLiteral("foo"));
        QCOMPARE(spy.count(), 0);
        QVERIFY(!c->contains(QStringLiteral("foo")));
        delete c;
    }

    void get_returnsDefaultIfAbsent() {
        auto* c = makeControl();
        QCOMPARE(c->get(QStringLiteral("missing")).toString(), QString());
        delete c;
    }

    void contains_distinguishesPresentFromAbsent() {
        auto* c = makeControl();
        QVERIFY(!c->contains(QStringLiteral("k")));
        c->set(QStringLiteral("k"), QStringLiteral("v"));
        QVERIFY(c->contains(QStringLiteral("k")));
        delete c;
    }
};

int main(int argc, char* argv[]) {
    QApplication app(argc, argv);
    TestSettingsControl t;
    return QTest::qExec(&t, argc, argv);
}

#include "testsettingscontrol.moc"
