// AppContainer.cpp
// Composition root implementation.

#include "AppContainer.h"

#include "../core/database/Database.h"
#include "../core/database/DatabaseConfig.h"
#include "../core/database/LibraryRepository.h"
#include "../core/database/LibraryScanner.h"
#include "../core/di/ServiceCollection.h"
#include "../core/playback/MediaKeyFilter.h"
#include "../core/playback/PlaybackService.h"
#include "../core/playback/QueueManager.h"
#include "../core/playback/SmtcController.h"
#include "../core/services/ArtworkEnrichment.h"
#include "../core/services/BrowseService.h"
#include "../core/services/DownloadService.h"
#include "../core/services/ExtensionManager.h"
#include "../core/services/HealthCheckService.h"
#include "../core/services/ImageCache.h"
#include "../core/services/LibraryService.h"
#include "../core/services/NavigationService.h"
#include "../core/services/SearchHistoryService.h"
#include "../core/services/SettingsControl.h"
#include "../core/services/SleepTimer.h"
#include "../core/services/ToastService.h"
#include "../core/services/LyricsService.h"
#include "../core/services/ScrobbleService.h"
#include "../core/services/ArtistAlbumService.h"
#include "../core/services/ReplayGainService.h"
#include "../core/services/EqualizerService.h"
#include "../core/services/ExtensionRepoService.h"
#include "../core/services/YouTubeThumbnailHelper.h"
#include "../core/sources/HttpClient.h"
#include "../core/sources/StreamingSourceManager.h"
#include "../core/sources/SubsonicProvider.h"
#include "../core/sources/YouTubeProvider.h"
#include "../core/theme/ThemeManager.h"
#include "viewmodels/AlbumViewModel.h"
#include "viewmodels/ArtistViewModel.h"
#include "viewmodels/DiscoverViewModel.h"
#include "viewmodels/HomeViewModel.h"
#include "viewmodels/LibraryViewModel.h"
#include "viewmodels/MainViewModel.h"
#include "viewmodels/PlayerViewModel.h"
#include "viewmodels/PlaylistViewModel.h"
#include "viewmodels/SearchViewModel.h"

#include <QCoreApplication>
#include <QDebug>

namespace mf::app {

using mf::core::di::ServiceCollection;

AppContainer::AppContainer()
    : services_(std::make_unique<ServiceCollection>())
{
}

AppContainer::~AppContainer() = default;

void AppContainer::build() {
    if (built_) return;

    auto& s = *services_;
    qInfo() << "AppContainer::build() — start";

    // ── Database ───────────────────────────────────────────────────────
    qInfo() << "AppContainer::build() — registering Database";
    s.registerFactory<mf::core::database::Database, mf::core::database::Database>(
        []() -> std::shared_ptr<mf::core::database::Database> {
            auto db = std::make_shared<mf::core::database::Database>(
                mf::core::database::DatabaseConfig::defaultConfig());
            db->open();
            return db;
        }
    );

    // ── Library repository (depends on Database) ───────────────────────
    qInfo() << "AppContainer::build() — registering LibraryRepository";
    s.registerFactory<mf::core::database::LibraryRepository,
                      mf::core::database::LibraryRepository>(
        [this]() -> std::shared_ptr<mf::core::database::LibraryRepository> {
            return std::make_shared<mf::core::database::LibraryRepository>(*database());
        }
    );

    // ── HTTP client (shared by all streaming providers) ────────────────
    qInfo() << "AppContainer::build() — registering HttpClient";
    s.registerType<mf::core::sources::HttpClient,
                   mf::core::sources::HttpClient>(ServiceCollection::Lifetime::Singleton);

    // ── Streaming source manager (registry of providers) ───────────────
    qInfo() << "AppContainer::build() — registering StreamingSourceManager";
    s.registerType<mf::core::sources::StreamingSourceManager,
                   mf::core::sources::StreamingSourceManager>(ServiceCollection::Lifetime::Singleton);

    // ── Providers ──────────────────────────────────────────────────────
    qInfo() << "AppContainer::build() — registering SubsonicProvider";
    s.registerFactory<mf::core::sources::SubsonicProvider,
                      mf::core::sources::SubsonicProvider>(
        [this]() -> std::shared_ptr<mf::core::sources::SubsonicProvider> {
            return std::make_shared<mf::core::sources::SubsonicProvider>(http().get());
        }
    );
    qInfo() << "AppContainer::build() — registering YouTubeProvider";
    s.registerFactory<mf::core::sources::YouTubeProvider,
                      mf::core::sources::YouTubeProvider>(
        [this]() -> std::shared_ptr<mf::core::sources::YouTubeProvider> {
            return std::make_shared<mf::core::sources::YouTubeProvider>(http().get());
        }
    );

    // ── Playback ───────────────────────────────────────────────────────
    qInfo() << "AppContainer::build() — registering PlaybackService";
    s.registerType<mf::core::playback::PlaybackService,
                   mf::core::playback::PlaybackService>(ServiceCollection::Lifetime::Singleton);
    qInfo() << "AppContainer::build() — registering QueueManager";
    s.registerType<mf::core::playback::QueueManager,
                   mf::core::playback::QueueManager>(ServiceCollection::Lifetime::Singleton);
    qInfo() << "AppContainer::build() — registering MediaKeyFilter";
    s.registerType<mf::core::playback::MediaKeyFilter,
                   mf::core::playback::MediaKeyFilter>(ServiceCollection::Lifetime::Singleton);
    qInfo() << "AppContainer::build() — registering SmtcController";
    s.registerType<mf::core::playback::SmtcController,
                   mf::core::playback::SmtcController>(ServiceCollection::Lifetime::Singleton);

    // ── Services ───────────────────────────────────────────────────────
    qInfo() << "AppContainer::build() — registering SettingsControl";
    s.registerType<mf::core::services::SettingsControl,
                   mf::core::services::SettingsControl>(ServiceCollection::Lifetime::Singleton);
    qInfo() << "AppContainer::build() — registering SearchHistoryService";
    s.registerFactory<mf::core::services::SearchHistoryService,
                      mf::core::services::SearchHistoryService>(
        [this]() -> std::shared_ptr<mf::core::services::SearchHistoryService> {
            return std::make_shared<mf::core::services::SearchHistoryService>(library().get());
        }
    );
    qInfo() << "AppContainer::build() — registering HealthCheckService";
    s.registerFactory<mf::core::services::HealthCheckService,
                      mf::core::services::HealthCheckService>(
        [this]() -> std::shared_ptr<mf::core::services::HealthCheckService> {
            return std::make_shared<mf::core::services::HealthCheckService>(sourceManager().get(), nullptr);
        }
    );
    qInfo() << "AppContainer::build() — registering BrowseService";
    s.registerFactory<mf::core::services::BrowseService,
                      mf::core::services::BrowseService>(
        [this]() -> std::shared_ptr<mf::core::services::BrowseService> {
            return std::make_shared<mf::core::services::BrowseService>(sourceManager().get(), nullptr);
        }
    );
    qInfo() << "AppContainer::build() — registering ExtensionManager";
    s.registerType<mf::core::services::ExtensionManager,
                   mf::core::services::ExtensionManager>(ServiceCollection::Lifetime::Singleton);
    qInfo() << "AppContainer::build() — registering DownloadService";
    s.registerType<mf::core::services::DownloadService,
                   mf::core::services::DownloadService>(ServiceCollection::Lifetime::Singleton);
    qInfo() << "AppContainer::build() — registering LibraryService";
    s.registerFactory<mf::core::services::LibraryService,
                      mf::core::services::LibraryService>(
        [this]() -> std::shared_ptr<mf::core::services::LibraryService> {
            return std::make_shared<mf::core::services::LibraryService>(library().get());
        }
    );
    qInfo() << "AppContainer::build() — registering NavigationService";
    s.registerType<mf::core::services::NavigationService,
                   mf::core::services::NavigationService>(ServiceCollection::Lifetime::Singleton);
    qInfo() << "AppContainer::build() — registering ToastService";
    s.registerType<mf::core::services::ToastService,
                   mf::core::services::ToastService>(ServiceCollection::Lifetime::Singleton);

    // ── Sleep timer (depends on PlaybackService) ──────────────────────
    qInfo() << "AppContainer::build() — registering SleepTimer";
    s.registerFactory<mf::core::services::SleepTimer,
                      mf::core::services::SleepTimer>(
        [this]() -> std::shared_ptr<mf::core::services::SleepTimer> {
            return std::make_shared<mf::core::services::SleepTimer>(
                playback().get());
        }
    );

    // ── Image cache + artwork enrichment ───────────────────────────────
    qInfo() << "AppContainer::build() — registering ImageCache";
    s.registerFactory<mf::core::services::ImageCache,
                      mf::core::services::ImageCache>(
        [this]() -> std::shared_ptr<mf::core::services::ImageCache> {
            return std::make_shared<mf::core::services::ImageCache>(
                http().get());
        }
    );
    qInfo() << "AppContainer::build() — registering ArtworkEnrichment";
    s.registerFactory<mf::core::services::ArtworkEnrichment,
                      mf::core::services::ArtworkEnrichment>(
        [this]() -> std::shared_ptr<mf::core::services::ArtworkEnrichment> {
            return std::make_shared<mf::core::services::ArtworkEnrichment>(
                imageCache().get());
        }
    );

    // ── Lyrics service (depends on HttpClient) ────────────────────────
    qInfo() << "AppContainer::build() — registering LyricsService";
    s.registerFactory<mf::core::services::LyricsService,
                      mf::core::services::LyricsService>(
        [this]() -> std::shared_ptr<mf::core::services::LyricsService> {
            return std::make_shared<mf::core::services::LyricsService>(
                http().get());
        }
    );

    // ── Scrobble service (depends on HttpClient + SettingsControl) ────
    qInfo() << "AppContainer::build() — registering ScrobbleService";
    s.registerFactory<mf::core::services::ScrobbleService,
                      mf::core::services::ScrobbleService>(
        [this]() -> std::shared_ptr<mf::core::services::ScrobbleService> {
            return std::make_shared<mf::core::services::ScrobbleService>(
                http().get(), settings().get());
        }
    );

    // ── Artist/Album service (depends on LibraryRepository + BrowseService) ──
    qInfo() << "AppContainer::build() — registering ArtistAlbumService";
    s.registerFactory<mf::core::services::ArtistAlbumService,
                      mf::core::services::ArtistAlbumService>(
        [this]() -> std::shared_ptr<mf::core::services::ArtistAlbumService> {
            return std::make_shared<mf::core::services::ArtistAlbumService>(
                library().get(), browse().get());
        }
    );

    // ── ReplayGain service (standalone, reads tags from files) ────────
    qInfo() << "AppContainer::build() — registering ReplayGainService";
    s.registerType<mf::core::services::ReplayGainService,
                   mf::core::services::ReplayGainService>(ServiceCollection::Lifetime::Singleton);

    // ── Equalizer service (standalone, preset-based EQ) ──────────────
    qInfo() << "AppContainer::build() — registering EqualizerService";
    s.registerType<mf::core::services::EqualizerService,
                   mf::core::services::EqualizerService>(ServiceCollection::Lifetime::Singleton);

    // ── Extension repo service (fetches manifests from remote repos) ──
    qInfo() << "AppContainer::build() — registering ExtensionRepoService";
    s.registerFactory<mf::core::services::ExtensionRepoService,
                      mf::core::services::ExtensionRepoService>(
        [this]() -> std::shared_ptr<mf::core::services::ExtensionRepoService> {
            return std::make_shared<mf::core::services::ExtensionRepoService>(
                http().get(), settings().get());
        }
    );

    // ── YouTube thumbnail helper (depends on ImageCache) ─────────────
    qInfo() << "AppContainer::build() — registering YouTubeThumbnailHelper";
    s.registerFactory<mf::core::services::YouTubeThumbnailHelper,
                      mf::core::services::YouTubeThumbnailHelper>(
        [this]() -> std::shared_ptr<mf::core::services::YouTubeThumbnailHelper> {
            return std::make_shared<mf::core::services::YouTubeThumbnailHelper>(
                imageCache().get());
        }
    );

    // ── Theme ──────────────────────────────────────────────────────────
    qInfo() << "AppContainer::build() — registering ThemeManager";
    s.registerFactory<mf::core::theme::ThemeManager,
                      mf::core::theme::ThemeManager>(
        [this]() -> std::shared_ptr<mf::core::theme::ThemeManager> {
            auto tm = std::make_shared<mf::core::theme::ThemeManager>();
            tm->bindSettings(settings().get());
            tm->loadFromSettings();
            return tm;
        }
    );

    // ── ViewModels ────────────────────────────────────────────────────
    qInfo() << "AppContainer::build() — registering ViewModels";
    s.registerFactory<mf::app::viewmodels::PlayerViewModel,
                      mf::app::viewmodels::PlayerViewModel>(
        [this]() -> std::shared_ptr<mf::app::viewmodels::PlayerViewModel> {
            auto vm = std::make_shared<mf::app::viewmodels::PlayerViewModel>(
                playback().get(), queue().get(), library().get());
            vm->setLyricsService(lyrics().get());
            vm->setScrobbler(scrobbler().get());
            vm->setReplayGain(replayGain().get());
            vm->setEqualizer(equalizer().get());
            return vm;
        }
    );
    s.registerFactory<mf::app::viewmodels::LibraryViewModel,
                      mf::app::viewmodels::LibraryViewModel>(
        [this]() -> std::shared_ptr<mf::app::viewmodels::LibraryViewModel> {
            return std::make_shared<mf::app::viewmodels::LibraryViewModel>(
                library().get(), queue().get());
        }
    );
    s.registerType<mf::app::viewmodels::MainViewModel,
                   mf::app::viewmodels::MainViewModel>(ServiceCollection::Lifetime::Singleton);
    s.registerFactory<mf::app::viewmodels::HomeViewModel,
                      mf::app::viewmodels::HomeViewModel>(
        [this]() -> std::shared_ptr<mf::app::viewmodels::HomeViewModel> {
            return std::make_shared<mf::app::viewmodels::HomeViewModel>(
                library().get(),
                queue().get(),
                libraryService().get(),
                toasts().get(),
                nav().get());
        }
    );
    s.registerFactory<mf::app::viewmodels::AlbumViewModel,
                      mf::app::viewmodels::AlbumViewModel>(
        [this]() -> std::shared_ptr<mf::app::viewmodels::AlbumViewModel> {
            return std::make_shared<mf::app::viewmodels::AlbumViewModel>(queue().get());
        }
    );
    s.registerFactory<mf::app::viewmodels::PlaylistViewModel,
                      mf::app::viewmodels::PlaylistViewModel>(
        [this]() -> std::shared_ptr<mf::app::viewmodels::PlaylistViewModel> {
            return std::make_shared<mf::app::viewmodels::PlaylistViewModel>(queue().get(), library().get());
        }
    );
    s.registerFactory<mf::app::viewmodels::ArtistViewModel,
                      mf::app::viewmodels::ArtistViewModel>(
        [this]() -> std::shared_ptr<mf::app::viewmodels::ArtistViewModel> {
            return std::make_shared<mf::app::viewmodels::ArtistViewModel>(
                browse().get(), queue().get(), toasts().get());
        }
    );
    s.registerFactory<mf::app::viewmodels::DiscoverViewModel,
                      mf::app::viewmodels::DiscoverViewModel>(
        [this]() -> std::shared_ptr<mf::app::viewmodels::DiscoverViewModel> {
            return std::make_shared<mf::app::viewmodels::DiscoverViewModel>(
                browse().get(), queue().get(), toasts().get());
        }
    );
    s.registerFactory<mf::app::viewmodels::SearchViewModel,
                      mf::app::viewmodels::SearchViewModel>(
        [this]() -> std::shared_ptr<mf::app::viewmodels::SearchViewModel> {
            return std::make_shared<mf::app::viewmodels::SearchViewModel>(
                libraryVm().get(),
                sourceManager().get(),
                searchHistory().get(),
                queue().get(),
                nav().get());
        }
    );

    // ── Register default providers ────────────────────────────────────
    qInfo() << "AppContainer::build() — registering default providers";
    auto sm = sourceManager();
    sm->registerProvider(s.resolve<mf::core::sources::SubsonicProvider>());
    sm->registerProvider(s.resolve<mf::core::sources::YouTubeProvider>());

    qInfo() << "AppContainer::build() — done";
    built_ = true;
}

// ── Typed accessors ──────────────────────────────────────────────────

std::shared_ptr<mf::core::database::Database> AppContainer::database() const {
    return services_->resolve<mf::core::database::Database>();
}
std::shared_ptr<mf::core::database::LibraryRepository> AppContainer::library() const {
    return services_->resolve<mf::core::database::LibraryRepository>();
}
std::shared_ptr<mf::core::playback::PlaybackService> AppContainer::playback() const {
    return services_->resolve<mf::core::playback::PlaybackService>();
}
std::shared_ptr<mf::core::playback::QueueManager> AppContainer::queue() const {
    return services_->resolve<mf::core::playback::QueueManager>();
}
std::shared_ptr<mf::core::playback::MediaKeyFilter> AppContainer::mediaKeys() const {
    return services_->resolve<mf::core::playback::MediaKeyFilter>();
}
std::shared_ptr<mf::core::playback::SmtcController> AppContainer::smtc() const {
    return services_->resolve<mf::core::playback::SmtcController>();
}
std::shared_ptr<mf::core::services::BrowseService> AppContainer::browse() const {
    return services_->resolve<mf::core::services::BrowseService>();
}
std::shared_ptr<mf::core::services::SearchHistoryService> AppContainer::searchHistory() const {
    return services_->resolve<mf::core::services::SearchHistoryService>();
}
std::shared_ptr<mf::core::services::HealthCheckService> AppContainer::health() const {
    return services_->resolve<mf::core::services::HealthCheckService>();
}
std::shared_ptr<mf::core::services::SettingsControl> AppContainer::settings() const {
    return services_->resolve<mf::core::services::SettingsControl>();
}
std::shared_ptr<mf::core::services::ExtensionManager> AppContainer::extensions() const {
    return services_->resolve<mf::core::services::ExtensionManager>();
}
std::shared_ptr<mf::core::services::DownloadService> AppContainer::downloads() const {
    return services_->resolve<mf::core::services::DownloadService>();
}
std::shared_ptr<mf::core::services::LibraryService> AppContainer::libraryService() const {
    return services_->resolve<mf::core::services::LibraryService>();
}
std::shared_ptr<mf::core::sources::HttpClient> AppContainer::http() const {
    return services_->resolve<mf::core::sources::HttpClient>();
}
std::shared_ptr<mf::core::sources::StreamingSourceManager> AppContainer::sourceManager() const {
    return services_->resolve<mf::core::sources::StreamingSourceManager>();
}
std::shared_ptr<mf::core::theme::ThemeManager> AppContainer::theme() const {
    return services_->resolve<mf::core::theme::ThemeManager>();
}
std::shared_ptr<mf::app::viewmodels::PlayerViewModel> AppContainer::playerVm() const {
    return services_->resolve<mf::app::viewmodels::PlayerViewModel>();
}
std::shared_ptr<mf::app::viewmodels::LibraryViewModel> AppContainer::libraryVm() const {
    return services_->resolve<mf::app::viewmodels::LibraryViewModel>();
}
std::shared_ptr<mf::app::viewmodels::MainViewModel> AppContainer::mainVm() const {
    return services_->resolve<mf::app::viewmodels::MainViewModel>();
}
std::shared_ptr<mf::app::viewmodels::HomeViewModel> AppContainer::homeVm() const {
    return services_->resolve<mf::app::viewmodels::HomeViewModel>();
}
std::shared_ptr<mf::app::viewmodels::AlbumViewModel> AppContainer::albumVm() const {
    return services_->resolve<mf::app::viewmodels::AlbumViewModel>();
}
std::shared_ptr<mf::app::viewmodels::PlaylistViewModel> AppContainer::playlistVm() const {
    return services_->resolve<mf::app::viewmodels::PlaylistViewModel>();
}
std::shared_ptr<mf::app::viewmodels::ArtistViewModel> AppContainer::artistVm() const {
    return services_->resolve<mf::app::viewmodels::ArtistViewModel>();
}
std::shared_ptr<mf::app::viewmodels::DiscoverViewModel> AppContainer::discoverVm() const {
    return services_->resolve<mf::app::viewmodels::DiscoverViewModel>();
}
std::shared_ptr<mf::app::viewmodels::SearchViewModel> AppContainer::searchVm() const {
    return services_->resolve<mf::app::viewmodels::SearchViewModel>();
}
std::shared_ptr<mf::core::services::NavigationService> AppContainer::nav() const {
    return services_->resolve<mf::core::services::NavigationService>();
}
std::shared_ptr<mf::core::services::ToastService> AppContainer::toasts() const {
    return services_->resolve<mf::core::services::ToastService>();
}
std::shared_ptr<mf::core::services::ImageCache> AppContainer::imageCache() const {
    return services_->resolve<mf::core::services::ImageCache>();
}
std::shared_ptr<mf::core::services::ArtworkEnrichment> AppContainer::artwork() const {
    return services_->resolve<mf::core::services::ArtworkEnrichment>();
}
std::shared_ptr<mf::core::services::SleepTimer> AppContainer::sleepTimer() const {
    return services_->resolve<mf::core::services::SleepTimer>();
}
std::shared_ptr<mf::core::services::LyricsService> AppContainer::lyrics() const {
    return services_->resolve<mf::core::services::LyricsService>();
}
std::shared_ptr<mf::core::services::ScrobbleService> AppContainer::scrobbler() const {
    return services_->resolve<mf::core::services::ScrobbleService>();
}
std::shared_ptr<mf::core::services::ArtistAlbumService> AppContainer::artistAlbum() const {
    return services_->resolve<mf::core::services::ArtistAlbumService>();
}
std::shared_ptr<mf::core::services::ReplayGainService> AppContainer::replayGain() const {
    return services_->resolve<mf::core::services::ReplayGainService>();
}
std::shared_ptr<mf::core::services::EqualizerService> AppContainer::equalizer() const {
    return services_->resolve<mf::core::services::EqualizerService>();
}
std::shared_ptr<mf::core::services::ExtensionRepoService> AppContainer::extensionRepos() const {
    return services_->resolve<mf::core::services::ExtensionRepoService>();
}

std::shared_ptr<mf::core::services::YouTubeThumbnailHelper> AppContainer::youTubeThumbnail() const {
    return services_->resolve<mf::core::services::YouTubeThumbnailHelper>();
}

} // namespace mf::app
