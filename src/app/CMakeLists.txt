# src/app/CMakeLists.txt
# App composition root + main window. Built as a static library so
# the test target can also link to it (for end-to-end DI verification).

set(MUSICEFY_APP_SOURCES
    AppContainer.cpp
    AppLifecycle.cpp
    MainWindow.cpp
    viewmodels/HomeViewModel.cpp
    viewmodels/PlayerViewModel.cpp
    viewmodels/LibraryViewModel.cpp
    viewmodels/MainViewModel.cpp
    viewmodels/AlbumViewModel.cpp
    viewmodels/PlaylistViewModel.cpp
    viewmodels/ArtistViewModel.cpp
    viewmodels/DiscoverViewModel.cpp
    viewmodels/SearchViewModel.cpp
    viewmodels/SourceViewModel.cpp
    widgets/NowPlayingBar.cpp
    widgets/LibraryView.cpp
    widgets/Sidebar.cpp
    widgets/HomeView.cpp
    widgets/SearchView.cpp
    widgets/SettingsPage.cpp
    widgets/LibrarySettingsPanel.cpp
    widgets/AppearancePanel.cpp
    widgets/DownloadsSettingsPanel.cpp
    widgets/DiscoverSettingsPanel.cpp
    widgets/ExtensionsSettingsPanel.cpp
    widgets/RepositoriesSettingsPanel.cpp
    widgets/SourcesSettingsPanel.cpp
    widgets/AddSourceDialog.cpp
    widgets/PlaylistPickerDialog.cpp
    widgets/CreatePlaylistDialog.cpp
    widgets/CardColorGlowBehavior.cpp
    widgets/ArtistView.cpp
    widgets/AlbumView.cpp
    widgets/PlaylistView.cpp
    widgets/NowPlayingView.cpp
    widgets/LyricsPanel.cpp
    widgets/CoverImage.cpp
    widgets/ToastOverlay.cpp
    widgets/DiscoverView.cpp
    widgets/SvgIcon.cpp
    widgets/OverlayStackAnimator.cpp
    widgets/SplashScreen.cpp
    widgets/FolderLibraryControl.cpp
)

set(MUSICEFY_APP_HEADERS
    AppContainer.h
    AppLifecycle.h
    MainWindow.h
    viewmodels/HomeViewModel.h
    viewmodels/PlayerViewModel.h
    viewmodels/LibraryViewModel.h
    viewmodels/MainViewModel.h
    viewmodels/AlbumViewModel.h
    viewmodels/PlaylistViewModel.h
    viewmodels/ArtistViewModel.h
    viewmodels/DiscoverViewModel.h
    viewmodels/SearchViewModel.h
    viewmodels/SourceViewModel.h
    widgets/NowPlayingBar.h
    widgets/LibraryView.h
    widgets/Sidebar.h
    widgets/HomeView.h
    widgets/SearchView.h
    widgets/SettingsPage.h
    widgets/LibrarySettingsPanel.h
    widgets/AppearancePanel.h
    widgets/DownloadsSettingsPanel.h
    widgets/DiscoverSettingsPanel.h
    widgets/ExtensionsSettingsPanel.h
    widgets/RepositoriesSettingsPanel.h
    widgets/SourcesSettingsPanel.h
    widgets/AddSourceDialog.h
    widgets/PlaylistPickerDialog.h
    widgets/CreatePlaylistDialog.h
    widgets/CardColorGlowBehavior.h
    widgets/ArtistView.h
    widgets/AlbumView.h
    widgets/PlaylistView.h
    widgets/NowPlayingView.h
    widgets/LyricsPanel.h
    widgets/CoverImage.h
    widgets/ToastOverlay.h
    widgets/DiscoverView.h
    widgets/SvgIcon.h
    widgets/Icons.h
    widgets/OverlayStackAnimator.h
    widgets/SplashScreen.h
    widgets/FolderLibraryControl.h
)

add_library(musicefy_app STATIC
    ${MUSICEFY_APP_SOURCES}
    ${MUSICEFY_APP_HEADERS}
)

target_include_directories(musicefy_app PUBLIC
    ${CMAKE_CURRENT_SOURCE_DIR}
    ${CMAKE_CURRENT_SOURCE_DIR}/viewmodels
    ${CMAKE_CURRENT_SOURCE_DIR}/widgets
)

target_link_libraries(musicefy_app PUBLIC
    musicefy_core
    Qt5::Core
    Qt5::Gui
    Qt5::Widgets
    Qt5::Svg
    Qt5::Qml
    Qt5::Quick
    Qt5::QuickControls2
)

musicefy_set_warnings(musicefy_app)

set_target_properties(musicefy_app PROPERTIES
    POSITION_INDEPENDENT_CODE ON
)

# ── Executable: just main.cpp + the static lib above ───────────────────
add_executable(musicefy
    main.cpp
    musicefy.manifest
    musicefy.rc
)
target_link_libraries(musicefy PRIVATE musicefy_app)

musicefy_use_qt(musicefy)

# Copy migrations next to the executable for runtime lookup.
add_custom_command(TARGET musicefy POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E copy_directory
        ${CMAKE_SOURCE_DIR}/src/core/database/migrations
        $<TARGET_FILE_DIR:musicefy>/migrations
)

message(STATUS "  - musicefy (app) + musicefy_app (lib)")
