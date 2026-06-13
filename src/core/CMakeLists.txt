# core/CMakeLists.txt
# Musicefy.Core: pure C++ + Qt Core types, models, interfaces, and services.
# No GUI dependencies. Linked by both the app and the test suite.

set(MUSICEFY_CORE_SOURCES
    models/MusicFile.cpp
    models/ArtistInfo.cpp
    models/AlbumInfo.cpp
    models/StreamingSource.cpp
    models/SourceConfigField.cpp
    models/SourceHealthState.cpp
    models/SearchHistory.cpp
    models/SearchResultGroup.cpp
    models/SearchSourceMode.cpp
    models/BrowseSection.cpp
    models/HomeSection.cpp
    models/ExtensionManifest.cpp
    models/AutoPlaylistInfo.cpp
    models/SourceTypes.cpp
    models/MusicFileExtensions.cpp

    theme/ThemeMode.cpp
    theme/HctFacade.cpp
    theme/MusicefyColorScheme.cpp
    theme/AppThemeColorSchemes.cpp
    theme/ThemeManager.cpp

    di/ServiceCollection.cpp

    database/DatabaseConfig.cpp
    database/Database.cpp
    database/LibraryScanner.cpp
    database/TagReader.cpp
    database/LibraryRepository.cpp

    playback/PlaybackService.cpp
    playback/QueueManager.cpp
    playback/MediaKeyFilter.cpp
    playback/SmtcController.cpp

    sources/HttpClient.cpp
    sources/LocalFolderProvider.cpp
    sources/SubsonicProvider.cpp
    sources/YouTubeProvider.cpp
    sources/StreamingSourceManager.cpp
    sources/youtube/YouTubeUrlParser.cpp
    sources/youtube/Cipher.cpp
    sources/youtube/StreamCache.cpp
    sources/youtube/PlayerResponse.cpp
    sources/youtube/YouTubeExtractor.cpp
    sources/youtube/InnerTubeClient.cpp
    sources/youtube/JsCipherExtractor.cpp
    sources/youtube/YtDlpProcess.cpp
    sources/youtube/BotDetectionMitigator.cpp

    services/SettingsControl.cpp
    services/SleepTimer.cpp
    services/SearchHistoryService.cpp
    services/HealthCheckService.cpp
    services/BrowseService.cpp
    services/ExtensionManager.cpp
    services/DownloadService.cpp
    services/LibraryService.cpp
    services/NavigationService.cpp
    services/ToastService.cpp
    services/ImageCache.cpp
    services/ColorExtractor.cpp
    services/ArtworkEnrichment.cpp
    services/PathToImage.cpp
    services/ScrobbleService.cpp
    services/ArtistAlbumService.cpp
    services/LyricsService.cpp
    services/ReplayGainService.cpp
    services/EqualizerService.cpp
    services/ExtensionRepoService.cpp
    services/YouTubeThumbnailHelper.cpp
)

set(MUSICEFY_CORE_HEADERS
    models/MusicFile.h
    models/ArtistInfo.h
    models/AlbumInfo.h
    models/PlaylistInfo.h
    models/Playlist.h
    models/StreamingSource.h
    models/SourceConfigField.h
    models/SourceHealthState.h
    models/SearchHistory.h
    models/SearchResultGroup.h
    models/SearchSourceMode.h
    models/BrowseSection.h
    models/HomeSection.h
    models/ExtensionManifest.h
    models/AutoPlaylistInfo.h
    models/SourceTypes.h
    models/MusicFileExtensions.h

    interfaces/IAudioPlayer.h
    interfaces/ILibraryService.h
    interfaces/IQueueManager.h
    interfaces/IMusicSourceProvider.h
    interfaces/IMusicSourceSession.h
    interfaces/IStreamingSourceManager.h
    interfaces/IBrowseService.h
    interfaces/IHealthCheckService.h
    interfaces/IExtensionManager.h
    interfaces/IDownloadService.h
    interfaces/ISearchHistoryService.h
    interfaces/ISettingsControl.h
    interfaces/IFolderDataProvider.h

    theme/AppTheme.h
    theme/ThemeMode.h
    theme/MusicefyColorScheme.h
    theme/AppThemeColorSchemes.h
    theme/HctFacade.h
    theme/ThemeManager.h

    di/ServiceCollection.h

    database/DatabaseConfig.h
    database/Database.h
    database/LibraryScanner.h
    database/LibraryRepository.h

    playback/PlaybackService.h
    playback/QueueManager.h
    playback/MediaKeyFilter.h
    playback/SmtcController.h

    sources/HttpClient.h
    sources/LocalFolderProvider.h
    sources/SubsonicProvider.h
    sources/YouTubeProvider.h
    sources/StreamingSourceManager.h
    sources/youtube/YouTubeUrlParser.h
    sources/youtube/Cipher.h
    sources/youtube/StreamCache.h
    sources/youtube/PlayerResponse.h
    sources/youtube/YouTubeExtractor.h
    sources/youtube/InnerTubeClient.h
    sources/youtube/JsCipherExtractor.h
    sources/youtube/YtDlpProcess.h
    sources/youtube/BotDetectionMitigator.h

    services/SettingsControl.h
    services/SleepTimer.h
    services/SearchHistoryService.h
    services/HealthCheckService.h
    services/BrowseService.h
    services/ExtensionManager.h
    services/DownloadService.h
    services/LibraryService.h
    services/NavigationService.h
    services/ToastService.h
    services/ImageCache.h
    services/ColorExtractor.h
    services/ArtworkEnrichment.h
    services/PathToImage.h
    services/LyricsService.h
    services/ScrobbleService.h
    services/ArtistAlbumService.h
    services/ReplayGainService.h
    services/EqualizerService.h
    services/ExtensionRepoService.h
    services/YouTubeThumbnailHelper.h
)

add_library(musicefy_core STATIC
    ${MUSICEFY_CORE_SOURCES}
    ${MUSICEFY_CORE_HEADERS}
    ${CMAKE_SOURCE_DIR}/third_party/material_color_utilities/cpp/utils/utils.cc
    ${CMAKE_SOURCE_DIR}/third_party/material_color_utilities/cpp/cam/cam.cc
    ${CMAKE_SOURCE_DIR}/third_party/material_color_utilities/cpp/cam/viewing_conditions.cc
    ${CMAKE_SOURCE_DIR}/third_party/material_color_utilities/cpp/cam/hct.cc
    ${CMAKE_SOURCE_DIR}/third_party/material_color_utilities/cpp/cam/hct_solver.cc
    ${CMAKE_SOURCE_DIR}/third_party/material_color_utilities/cpp/palettes/tonal_palette.cc
    ${CMAKE_SOURCE_DIR}/third_party/material_color_utilities/cpp/dynamiccolor/dynamic_scheme.cc
)

target_include_directories(musicefy_core PUBLIC
    ${CMAKE_CURRENT_SOURCE_DIR}
    ${CMAKE_SOURCE_DIR}/third_party/material_color_utilities
)

target_link_libraries(musicefy_core PUBLIC
    Qt5::Core
    Qt5::Gui
    Qt5::Sql
    Qt5::Multimedia
    Qt5::Network
    Qt5::Qml
)

# Optional C++/WinRT linkage (MUSICEFY_ENABLE_WINRT_SRTC).
# Pulls in runtimeobject.lib (RoInitialize, RoUninitialize, etc.) and the
# FetchContent-fetched WinRT headers. Only active on Win 10+ and when the
# flag is on.
if(MUSICEFY_ENABLE_WINRT_SRTC AND WIN32)
    if(DEFINED MUSICEFY_CPPWINRT_INCLUDE_DIR)
        target_include_directories(musicefy_core PRIVATE
            ${MUSICEFY_CPPWINRT_INCLUDE_DIR})
    endif()
    target_link_libraries(musicefy_core PUBLIC runtimeobject)
    target_compile_definitions(musicefy_core PUBLIC
        MUSICEFY_ENABLE_WINRT_SRTC=1
        _WIN32_WINNT=0x0A00  # Win 10
    )
endif()

musicefy_set_warnings(musicefy_core)

set_target_properties(musicefy_core PROPERTIES
    POSITION_INDEPENDENT_CODE ON
    AUTOMOC ON
)

# Migration SQL files are looked up next to the executable at runtime,
# so copy them into the build dir so dev runs and tests work.
add_custom_command(TARGET musicefy_core POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E copy_directory
        ${CMAKE_CURRENT_SOURCE_DIR}/database/migrations
        $<TARGET_FILE_DIR:musicefy_core>/migrations
)

message(STATUS "  - musicefy_core: ${MUSICEFY_CORE_SOURCES} + ${MUSICEFY_CORE_HEADERS}")
