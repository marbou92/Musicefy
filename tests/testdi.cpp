// testdi.cpp
// Smoke tests for the ServiceCollection DI container.

#include "ServiceCollection.h"

#include <QTest>
#include <memory>

using mf::core::di::ServiceCollection;

namespace {
struct IService { virtual ~IService() = default; virtual int value() const = 0; };
struct ImplA : IService { int value() const override { return 42; } };
struct ImplB : IService { int value() const override { return 7; } };
}

class TestDi : public QObject {
    Q_OBJECT

private slots:
    void singletonResolvesSameInstance();
    void transientResolvesDifferentInstances();
    void resolveUnknownReturnsNull();
    void customFactoryTakesPrecedence();
    void clearRemovesRegistrations();
};

void TestDi::singletonResolvesSameInstance() {
    ServiceCollection sc;
    sc.registerType<IService, ImplA>(ServiceCollection::Lifetime::Singleton);
    auto a = sc.resolve<IService>();
    auto b = sc.resolve<IService>();
    QVERIFY(a != nullptr);
    QVERIFY(a.get() == b.get());
    QCOMPARE(a->value(), 42);
}

void TestDi::transientResolvesDifferentInstances() {
    ServiceCollection sc;
    sc.registerType<IService, ImplA>(ServiceCollection::Lifetime::Transient);
    auto a = sc.resolve<IService>();
    auto b = sc.resolve<IService>();
    QVERIFY(a.get() != b.get());
}

void TestDi::resolveUnknownReturnsNull() {
    ServiceCollection sc;
    auto a = sc.resolve<IService>();
    QCOMPARE(a, nullptr);
}

void TestDi::customFactoryTakesPrecedence() {
    ServiceCollection sc;
    sc.registerType<IService, ImplA>();
    sc.registerFactory<IService>([]() { return std::make_shared<ImplB>(); });
    auto a = sc.resolve<IService>();
    QCOMPARE(a->value(), 7);
}

void TestDi::clearRemovesRegistrations() {
    ServiceCollection sc;
    sc.registerType<IService, ImplA>();
    QVERIFY(sc.resolve<IService>() != nullptr);
    sc.clear();
    QCOMPARE(sc.resolve<IService>(), nullptr);
}

QTEST_GUILESS_MAIN(TestDi)
#include "testdi.moc"
