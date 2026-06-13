// testplaylistpickerdialog.cpp
// Unit tests for PlaylistPickerDialog: list population, selection,
// and accept/reject flows.

#include <QtTest/QtTest>
#include <QSignalSpy>
#include <QStandardItemModel>
#include <QTemporaryDir>
#include <QListView>
#include <QPushButton>
#include <QFile>
#include <QDir>
#include <QCoreApplication>

#include "core/database/Database.h"
#include "core/database/DatabaseConfig.h"
#include "core/database/LibraryRepository.h"
#include "core/playback/QueueManager.h"
#include "viewmodels/LibraryViewModel.h"
#include "widgets/PlaylistPickerDialog.h"
#include "core/theme/ThemeManager.h"

using mf::app::widgets::PlaylistPickerDialog;
using mf::app::viewmodels::LibraryViewModel;
using mf::core::database::Database;
using mf::core::database::DatabaseConfig;
using mf::core::database::LibraryRepository;
using mf::core::playback::QueueManager;
using mf::core::theme::ThemeManager;

class TestPlaylistPickerDialog : public QObject {
    Q_OBJECT

private:
    QTemporaryDir* tmpDir_ = nullptr;
    Database* db_ = nullptr;
    LibraryRepository* repo_ = nullptr;
    QueueManager* queue_ = nullptr;
    LibraryViewModel* libVm_ = nullptr;
    ThemeManager* theme_ = nullptr;

private slots:
    void initTestCase() {
        tmpDir_ = new QTemporaryDir;
        QVERIFY(tmpDir_->isValid());

        // Copy migrations
        QString migDir = tmpDir_->path() + QStringLiteral("/migrations");
        QDir().mkpath(migDir);
        QString src = QCoreApplication::applicationDirPath()
                      + QStringLiteral("/migrations/0001_initial_schema.sql");
        QString dst = migDir + QStringLiteral("/0001_initial_schema.sql");
        if (QFile::exists(src) && !QFile::exists(dst))
            QFile::copy(src, dst);

        DatabaseConfig c;
        c.setFilePath(tmpDir_->path() + QStringLiteral("/test.db"));
        c.setMigrationFiles({migDir});
        db_ = new Database(c);
        QVERIFY(db_->open());

        repo_ = new LibraryRepository(*db_, this);
        queue_ = new QueueManager(repo_, this);
        libVm_ = new LibraryViewModel(repo_, queue_, this);
        theme_ = new ThemeManager(nullptr, this);
    }

    void cleanupTestCase() {
        delete theme_;
        delete libVm_;
        delete queue_;
        delete repo_;
        delete db_;
        delete tmpDir_;
    }

    void ctor_showsEmptyListWithNoPlaylists() {
        PlaylistPickerDialog dlg(libVm_, theme_);
        auto* listView = dlg.findChild<QListView*>();
        QVERIFY(listView);
        auto* model = qobject_cast<QStandardItemModel*>(listView->model());
        QVERIFY(model);
        QCOMPARE(model->rowCount(), 0);
    }

    void addBtn_disabledWithNoSelection() {
        PlaylistPickerDialog dlg(libVm_, theme_);
        auto* addBtn = dlg.findChild<QPushButton*>(QStringLiteral("addBtn"));
        QVERIFY(addBtn);
        QVERIFY(!addBtn->isEnabled());
    }

    void selectedPlaylistId_emptyByDefault() {
        PlaylistPickerDialog dlg(libVm_, theme_);
        QVERIFY(dlg.selectedPlaylistId().isEmpty());
    }

    void selectedPlaylistName_emptyByDefault() {
        PlaylistPickerDialog dlg(libVm_, theme_);
        QVERIFY(dlg.selectedPlaylistName().isEmpty());
    }

    void reject_returnsRejected() {
        PlaylistPickerDialog dlg(libVm_, theme_);
        QCOMPARE(dlg.exec(), static_cast<int>(QDialog::Rejected));
    }

    void selectedPlaylistId_afterReject_isEmpty() {
        PlaylistPickerDialog dlg(libVm_, theme_);
        dlg.reject();
        QVERIFY(dlg.selectedPlaylistId().isEmpty());
    }

    void windowTitle_isCorrect() {
        PlaylistPickerDialog dlg(libVm_, theme_);
        QCOMPARE(dlg.windowTitle(), QStringLiteral("Add to Playlist"));
    }

    void minimumSize_isSet() {
        PlaylistPickerDialog dlg(libVm_, theme_);
        QVERIFY(dlg.minimumWidth() >= 360);
        QVERIFY(dlg.minimumHeight() >= 420);
    }
};

QTEST_MAIN(TestPlaylistPickerDialog)
#include "testplaylistpickerdialog.moc"
