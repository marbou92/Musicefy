// NavigationService.cpp
// See header. Thin dispatcher: every slot just emits a signal.

#include "NavigationService.h"

namespace mf::core::services {

using mf::core::models::ArtistInfo;
using mf::core::models::AlbumInfo;
using mf::core::models::PlaylistInfo;

NavigationService::NavigationService(QObject* parent)
    : QObject(parent)
{
}

void NavigationService::requestPage(const QString& name) {
    emit pageRequested(name);
}
void NavigationService::requestHome()     { emit pageRequested(QStringLiteral("home")); }
void NavigationService::requestSearch()   { emit pageRequested(QStringLiteral("search")); }
void NavigationService::requestLibrary()  { emit pageRequested(QStringLiteral("library")); }
void NavigationService::requestSettings() { emit pageRequested(QStringLiteral("settings")); }
void NavigationService::navigateBack()    { emit backRequested(); }

void NavigationService::requestArtist(ArtistInfo info)    { emit artistNavigationRequested(std::move(info)); }
void NavigationService::requestAlbum(AlbumInfo info)      { emit albumNavigationRequested(std::move(info)); }
void NavigationService::requestPlaylist(PlaylistInfo info){ emit playlistNavigationRequested(std::move(info)); }

void NavigationService::closeOverlay() { emit overlayCloseRequested(); }

} // namespace mf::core::services
