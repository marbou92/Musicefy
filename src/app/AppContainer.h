// AppContainer.h
// Composition root. Builds the ServiceCollection with every long-lived
// service registered, then provides typed accessors for the most
// common ones.

#pragma once

#include "../core/di/ServiceCollection.h"

#include <memory>

namespace mf::core::database { class Database; class LibraryRepository; }
namespace mf::core::playback  { class PlaybackService; class QueueManager;
                                 class MediaKeyFilter; class SmtcController; }
namespace mf::core::services  { class BrowseService; class HealthCheckService;
                                 class SearchHistoryService; class SettingsControl;
                                 class ExtensionManager; class DownloadService;
                                 class LibraryService; class NavigationService;
                                 class ToastService; class ImageCache;
                                 class ArtworkEnrichment; class SleepTimer;
                                 class LyricsService; class ScrobbleService;
                                 class ArtistAlbumService;
                                 class ReplayGainService; class EqualizerService;
                                 class ExtensionRepoService; class YouTubeThumbnailHelper; }
namespace mf::core::sources   { class HttpClient; class StreamingSourceManager;
                                 class SubsonicProvider; class YouTubeProvider; }
namespace mf::app::viewmodels { class PlayerViewModel; class LibraryViewModel;
                                 class MainViewModel; class HomeViewModel;
                                 class AlbumViewModel; class PlaylistViewModel;
                                 class ArtistViewModel; class DiscoverViewModel;
                                 class SearchViewModel; }
namespace mf::core::theme     { class ThemeManager; }

namespace mf::app {

class AppContainer {
public:
    AppContainer();
    ~AppContainer();

    void build();

    mf::core::di::ServiceCollection& services() { return *services_; }
    const mf::core::di::ServiceCollection& services() const { return *services_; }

    // Typed accessors.
    std::shared_ptr<mf::core::database::Database>          database()          const;
    std::shared_ptr<mf::core::database::LibraryRepository> library()           const;
    std::shared_ptr<mf::core::playback::PlaybackService>   playback()          const;
    std::shared_ptr<mf::core::playback::QueueManager>      queue()             const;
    std::shared_ptr<mf::core::playback::MediaKeyFilter>    mediaKeys()         const;
    std::shared_ptr<mf::core::playback::SmtcController>    smtc()              const;
    std::shared_ptr<mf::core::services::BrowseService>     browse()            const;
    std::shared_ptr<mf::core::services::SearchHistoryService> searchHistory()  const;
    std::shared_ptr<mf::core::services::HealthCheckService>  health()          const;
    std::shared_ptr<mf::core::services::SettingsControl>     settings()        const;
    std::shared_ptr<mf::core::services::ExtensionManager>    extensions()      const;
    std::shared_ptr<mf::core::services::DownloadService>     downloads()       const;
    std::shared_ptr<mf::core::services::LibraryService>      libraryService()  const;
    std::shared_ptr<mf::core::services::NavigationService>   nav()             const;
    std::shared_ptr<mf::core::services::ToastService>        toasts()          const;
    std::shared_ptr<mf::core::services::ImageCache>         imageCache()      const;
    std::shared_ptr<mf::core::services::ArtworkEnrichment>  artwork()         const;
    std::shared_ptr<mf::core::services::SleepTimer>         sleepTimer()      const;
    std::shared_ptr<mf::core::services::LyricsService>      lyrics()          const;
    std::shared_ptr<mf::core::services::ScrobbleService>    scrobbler()       const;
    std::shared_ptr<mf::core::services::ArtistAlbumService> artistAlbum()     const;
    std::shared_ptr<mf::core::services::ReplayGainService>  replayGain()     const;
    std::shared_ptr<mf::core::services::EqualizerService>   equalizer()      const;
    std::shared_ptr<mf::core::services::ExtensionRepoService> extensionRepos() const;
    std::shared_ptr<mf::core::services::YouTubeThumbnailHelper> youTubeThumbnail() const;
    std::shared_ptr<mf::core::sources::HttpClient>           http()            const;
    std::shared_ptr<mf::core::sources::StreamingSourceManager> sourceManager() const;
    std::shared_ptr<mf::core::theme::ThemeManager>            theme()          const;
    std::shared_ptr<mf::app::viewmodels::PlayerViewModel>     playerVm()       const;
    std::shared_ptr<mf::app::viewmodels::LibraryViewModel>    libraryVm()      const;
    std::shared_ptr<mf::app::viewmodels::MainViewModel>       mainVm()         const;
    std::shared_ptr<mf::app::viewmodels::HomeViewModel>       homeVm()        const;
    std::shared_ptr<mf::app::viewmodels::AlbumViewModel>      albumVm()       const;
    std::shared_ptr<mf::app::viewmodels::PlaylistViewModel>   playlistVm()    const;
    std::shared_ptr<mf::app::viewmodels::ArtistViewModel>     artistVm()      const;
    std::shared_ptr<mf::app::viewmodels::DiscoverViewModel>   discoverVm()    const;
    std::shared_ptr<mf::app::viewmodels::SearchViewModel>    searchVm()     const;

private:
    std::unique_ptr<mf::core::di::ServiceCollection> services_;
    bool built_ = false;
};

} // namespace mf::app
