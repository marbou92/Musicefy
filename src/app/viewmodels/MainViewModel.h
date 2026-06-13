// MainViewModel.h
// Top-level navigation state. The QML/Widgets layer binds to
// currentPage to know which QStackedWidget index to show; the
// Sidebar widget fires setPage() when the user clicks a tab.
//
// All cross-page navigation that isn't just "switch tab" goes
// through NavigationService (typed Artist/Album/Playlist events
// for the overlay system, and requestPage() for direct routing).

#pragma once

#include <QObject>
#include <QString>

namespace mf::app::viewmodels {

class MainViewModel : public QObject {
    Q_OBJECT
    Q_PROPERTY(int     currentPage READ currentPage  NOTIFY currentPageChanged)
    Q_PROPERTY(QString currentPageName READ currentPageName NOTIFY currentPageChanged)

public:
    enum Page {
        PageHome     = 0,
        PageSearch   = 1,
        PageLibrary  = 2,
        PageSettings = 3,
        PageDiscover = 4,
        PageFolders  = 5,
    };
    Q_ENUM(Page)

    explicit MainViewModel(QObject* parent = nullptr);
    ~MainViewModel() override = default;

    int     currentPage()     const { return currentPage_; }
    QString currentPageName() const;

public slots:
    void setPage(int index);
    void setPageByName(const QString& name);

    void navigateToHome();
    void navigateToSearch();
    void navigateToLibrary();
    void navigateToSettings();
    void navigateToDiscover();
    void navigateToFolders();

signals:
    void currentPageChanged();
    void pageChangeRequested(int index);

private:
    int currentPage_ = PageHome;

    static QString pageNameFor(int index);
    static int     pageIndexFor(const QString& name);
};

} // namespace mf::app::viewmodels
