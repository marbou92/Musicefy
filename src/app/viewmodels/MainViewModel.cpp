// MainViewModel.cpp
// See header.

#include "MainViewModel.h"

namespace mf::app::viewmodels {

MainViewModel::MainViewModel(QObject* parent)
    : QObject(parent)
{
}

QString MainViewModel::currentPageName() const {
    return pageNameFor(currentPage_);
}

void MainViewModel::setPage(int index) {
    if (index < PageHome || index > PageFolders) return;
    if (index == currentPage_) return;
    currentPage_ = index;
    emit currentPageChanged();
    emit pageChangeRequested(index);
}

void MainViewModel::setPageByName(const QString& name) {
    int idx = pageIndexFor(name);
    if (idx >= 0) {
        setPage(idx);
    }
}

void MainViewModel::navigateToHome()     { setPage(PageHome); }
void MainViewModel::navigateToSearch()   { setPage(PageSearch); }
void MainViewModel::navigateToLibrary()  { setPage(PageLibrary); }
void MainViewModel::navigateToSettings() { setPage(PageSettings); }
void MainViewModel::navigateToDiscover() { setPage(PageDiscover); }
void MainViewModel::navigateToFolders()   { setPage(PageFolders); }

QString MainViewModel::pageNameFor(int index) {
    switch (index) {
        case PageHome:     return QStringLiteral("home");
        case PageSearch:   return QStringLiteral("search");
        case PageLibrary:  return QStringLiteral("library");
        case PageSettings: return QStringLiteral("settings");
        case PageDiscover: return QStringLiteral("discover");
        case PageFolders:  return QStringLiteral("folders");
        default:           return QStringLiteral("home");
    }
}

int MainViewModel::pageIndexFor(const QString& name) {
    if (name == QLatin1String("home"))     return PageHome;
    if (name == QLatin1String("search"))   return PageSearch;
    if (name == QLatin1String("library"))  return PageLibrary;
    if (name == QLatin1String("settings")) return PageSettings;
    if (name == QLatin1String("discover")) return PageDiscover;
    if (name == QLatin1String("folders"))  return PageFolders;
    return -1;
}

} // namespace mf::app::viewmodels
