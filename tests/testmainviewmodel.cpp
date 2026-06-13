// testmainviewmodel.cpp
// Verifies the top-level navigation state machine: page index round-
// trips, signals fire on changes, setPageByName maps all four names
// to the correct indices, and the by-name convenience slots land on
// the right page.

#include <QtTest/QtTest>
#include <QCoreApplication>
#include <QSettings>

#include "viewmodels/MainViewModel.h"

using mf::app::viewmodels::MainViewModel;

class TestMainViewModel : public QObject {
    Q_OBJECT

private:
    std::unique_ptr<MainViewModel> vm_;

private slots:
    void initTestCase() {
        QCoreApplication::setOrganizationName(QStringLiteral("MusicefyTest"));
        QCoreApplication::setApplicationName(QStringLiteral("mainviewmodel"));
    }
    void cleanupTestCase() { QSettings().clear(); }

    void initialStateIsHome() {
        vm_ = std::make_unique<MainViewModel>();
        QCOMPARE(vm_->currentPage(), int(MainViewModel::PageHome));
        QCOMPARE(vm_->currentPageName(), QStringLiteral("home"));
    }

    void setPageAdvancesAndEmits() {
        vm_ = std::make_unique<MainViewModel>();
        int changes = 0;
        int lastRow = -1;
        connect(vm_.get(), &MainViewModel::currentPageChanged,
                this, [&]() { ++changes; });
        connect(vm_.get(), &MainViewModel::pageChangeRequested,
                this, [&](int row) { lastRow = row; });

        vm_->setPage(MainViewModel::PageSearch);
        QCOMPARE(vm_->currentPage(), int(MainViewModel::PageSearch));
        QCOMPARE(vm_->currentPageName(), QStringLiteral("search"));
        QCOMPARE(changes, 1);
        QCOMPARE(lastRow, int(MainViewModel::PageSearch));
    }

    void setPageIgnoresSameValue() {
        vm_ = std::make_unique<MainViewModel>();
        int changes = 0;
        connect(vm_.get(), &MainViewModel::currentPageChanged,
                this, [&]() { ++changes; });
        vm_->setPage(MainViewModel::PageHome);  // already there
        QCOMPARE(changes, 0);
    }

    void setPageIgnoresOutOfRange() {
        vm_ = std::make_unique<MainViewModel>();
        int changes = 0;
        connect(vm_.get(), &MainViewModel::currentPageChanged,
                this, [&]() { ++changes; });
        vm_->setPage(99);
        vm_->setPage(-1);
        QCOMPARE(changes, 0);
        QCOMPARE(vm_->currentPage(), int(MainViewModel::PageHome));
    }

    void setPageByNameMapsAll() {
        vm_ = std::make_unique<MainViewModel>();
        vm_->setPageByName(QStringLiteral("home"));
        QCOMPARE(vm_->currentPage(), int(MainViewModel::PageHome));
        vm_->setPageByName(QStringLiteral("search"));
        QCOMPARE(vm_->currentPage(), int(MainViewModel::PageSearch));
        vm_->setPageByName(QStringLiteral("library"));
        QCOMPARE(vm_->currentPage(), int(MainViewModel::PageLibrary));
        vm_->setPageByName(QStringLiteral("settings"));
        QCOMPARE(vm_->currentPage(), int(MainViewModel::PageSettings));
        vm_->setPageByName(QStringLiteral("discover"));
        QCOMPARE(vm_->currentPage(), int(MainViewModel::PageDiscover));
    }

    void setPageByNameIgnoresUnknown() {
        vm_ = std::make_unique<MainViewModel>();
        int changes = 0;
        connect(vm_.get(), &MainViewModel::currentPageChanged,
                this, [&]() { ++changes; });
        vm_->setPageByName(QStringLiteral("nope"));
        QCOMPARE(changes, 0);
        QCOMPARE(vm_->currentPage(), int(MainViewModel::PageHome));
    }

    void convenienceSlotsWork() {
        vm_ = std::make_unique<MainViewModel>();
        vm_->navigateToSearch();
        QCOMPARE(vm_->currentPage(), int(MainViewModel::PageSearch));
        vm_->navigateToLibrary();
        QCOMPARE(vm_->currentPage(), int(MainViewModel::PageLibrary));
        vm_->navigateToSettings();
        QCOMPARE(vm_->currentPage(), int(MainViewModel::PageSettings));
        vm_->navigateToDiscover();
        QCOMPARE(vm_->currentPage(), int(MainViewModel::PageDiscover));
        vm_->navigateToHome();
        QCOMPARE(vm_->currentPage(), int(MainViewModel::PageHome));
    }
};

QTEST_MAIN(TestMainViewModel)
#include "testmainviewmodel.moc"
