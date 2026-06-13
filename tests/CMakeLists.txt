# tests/CMakeLists.txt
# Qt Test-based unit tests. Catches regressions in the foundation layer
# (models, HCT, DI) without requiring a full app build.

function(musicefy_add_test name)
    add_executable(${name} ${name}.cpp)
    target_link_libraries(${name} PRIVATE musicefy_core Qt5::Test)
    target_include_directories(${name} PRIVATE
        ${CMAKE_SOURCE_DIR}/src/core
        ${CMAKE_SOURCE_DIR}/third_party
    )
    musicefy_set_warnings(${name})
    add_test(NAME ${name} COMMAND ${name})
    set_tests_properties(${name} PROPERTIES
        ENVIRONMENT "QT_QPA_PLATFORM=offscreen"
        TIMEOUT 30
    )
    add_custom_command(TARGET ${name} POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E copy_directory
            ${CMAKE_SOURCE_DIR}/src/core/database/migrations
            $<TARGET_FILE_DIR:${name}>/migrations
    )
endfunction()

# The container test also needs the app's DI graph, which lives in
# musicefy_app. It pulls in Qt5::Widgets (via MainWindow) so we add
# it explicitly.
function(musicefy_add_app_test name)
    add_executable(${name} ${name}.cpp)
    target_link_libraries(${name} PRIVATE musicefy_app musicefy_core Qt5::Test)
    target_include_directories(${name} PRIVATE
        ${CMAKE_SOURCE_DIR}/src/app
        ${CMAKE_SOURCE_DIR}/src/core
        ${CMAKE_SOURCE_DIR}/third_party
    )
    musicefy_set_warnings(${name})
    add_test(NAME ${name} COMMAND ${name})
    set_tests_properties(${name} PROPERTIES
        ENVIRONMENT "QT_QPA_PLATFORM=offscreen"
        TIMEOUT 30
    )
    add_custom_command(TARGET ${name} POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E copy_directory
            ${CMAKE_SOURCE_DIR}/src/core/database/migrations
            $<TARGET_FILE_DIR:${name}>/migrations
    )
endfunction()

if(BUILD_TESTING)
    enable_testing()

    musicefy_add_test(testhct)
    musicefy_add_test(testmodels)
    musicefy_add_test(testdi)
    musicefy_add_test(testpalettes)
    musicefy_add_test(testtheme)
    musicefy_add_test(testdatabase)
    musicefy_add_test(testplayback)
    musicefy_add_test(testsources)
    musicefy_add_test(testservices)
    musicefy_add_app_test(testcontainer)
    # Viewmodel tests compile the viewmodel cpp directly so they
    # don't need the full app graph.
    add_executable(testplayerviewmodel
        testplayerviewmodel.cpp
        ${CMAKE_SOURCE_DIR}/src/app/viewmodels/PlayerViewModel.cpp
    )
    target_link_libraries(testplayerviewmodel PRIVATE
        musicefy_core
        Qt5::Test
    )
    target_include_directories(testplayerviewmodel PRIVATE
        ${CMAKE_SOURCE_DIR}/src/app
        ${CMAKE_SOURCE_DIR}/src/core
        ${CMAKE_SOURCE_DIR}/third_party
    )
    musicefy_set_warnings(testplayerviewmodel)
    add_test(NAME testplayerviewmodel COMMAND testplayerviewmodel)
    set_tests_properties(testplayerviewmodel PROPERTIES
        ENVIRONMENT "QT_QPA_PLATFORM=offscreen"
        TIMEOUT 30
    )

    add_executable(testlibraryviewmodel
        testlibraryviewmodel.cpp
        ${CMAKE_SOURCE_DIR}/src/app/viewmodels/LibraryViewModel.cpp
    )
    target_link_libraries(testlibraryviewmodel PRIVATE
        musicefy_core
        Qt5::Test
    )
    target_include_directories(testlibraryviewmodel PRIVATE
        ${CMAKE_SOURCE_DIR}/src/app
        ${CMAKE_SOURCE_DIR}/src/core
        ${CMAKE_SOURCE_DIR}/third_party
    )
    musicefy_set_warnings(testlibraryviewmodel)
    add_test(NAME testlibraryviewmodel COMMAND testlibraryviewmodel)
    set_tests_properties(testlibraryviewmodel PROPERTIES
        ENVIRONMENT "QT_QPA_PLATFORM=offscreen"
        TIMEOUT 30
    )
    add_custom_command(TARGET testlibraryviewmodel POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E copy_directory
            ${CMAKE_SOURCE_DIR}/src/core/database/migrations
            $<TARGET_FILE_DIR:testlibraryviewmodel>/migrations
    )

    add_executable(testalbumviewmodel
        testalbumviewmodel.cpp
        ${CMAKE_SOURCE_DIR}/src/app/viewmodels/AlbumViewModel.cpp
    )
    target_link_libraries(testalbumviewmodel PRIVATE
        musicefy_core
        Qt5::Test
    )
    target_include_directories(testalbumviewmodel PRIVATE
        ${CMAKE_SOURCE_DIR}/src/app
        ${CMAKE_SOURCE_DIR}/src/core
        ${CMAKE_SOURCE_DIR}/third_party
    )
    musicefy_set_warnings(testalbumviewmodel)
    add_test(NAME testalbumviewmodel COMMAND testalbumviewmodel)
    set_tests_properties(testalbumviewmodel PROPERTIES
        ENVIRONMENT "QT_QPA_PLATFORM=offscreen"
        TIMEOUT 30
    )
    add_custom_command(TARGET testalbumviewmodel POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E copy_directory
            ${CMAKE_SOURCE_DIR}/src/core/database/migrations
            $<TARGET_FILE_DIR:testalbumviewmodel>/migrations
    )

    add_executable(testplaylistviewmodel
        testplaylistviewmodel.cpp
        ${CMAKE_SOURCE_DIR}/src/app/viewmodels/PlaylistViewModel.cpp
    )
    target_link_libraries(testplaylistviewmodel PRIVATE
        musicefy_core
        Qt5::Test
    )
    target_include_directories(testplaylistviewmodel PRIVATE
        ${CMAKE_SOURCE_DIR}/src/app
        ${CMAKE_SOURCE_DIR}/src/core
        ${CMAKE_SOURCE_DIR}/third_party
    )
    musicefy_set_warnings(testplaylistviewmodel)
    add_test(NAME testplaylistviewmodel COMMAND testplaylistviewmodel)
    set_tests_properties(testplaylistviewmodel PROPERTIES
        ENVIRONMENT "QT_QPA_PLATFORM=offscreen"
        TIMEOUT 30
    )
    add_custom_command(TARGET testplaylistviewmodel POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E copy_directory
            ${CMAKE_SOURCE_DIR}/src/core/database/migrations
            $<TARGET_FILE_DIR:testplaylistviewmodel>/migrations
    )

    add_executable(testmainviewmodel
        testmainviewmodel.cpp
        ${CMAKE_SOURCE_DIR}/src/app/viewmodels/MainViewModel.cpp
    )
    target_link_libraries(testmainviewmodel PRIVATE
        musicefy_core
        Qt5::Test
    )
    target_include_directories(testmainviewmodel PRIVATE
        ${CMAKE_SOURCE_DIR}/src/app
        ${CMAKE_SOURCE_DIR}/src/core
        ${CMAKE_SOURCE_DIR}/third_party
    )
    musicefy_set_warnings(testmainviewmodel)
    add_test(NAME testmainviewmodel COMMAND testmainviewmodel)
    set_tests_properties(testmainviewmodel PROPERTIES
        ENVIRONMENT "QT_QPA_PLATFORM=offscreen"
        TIMEOUT 30
    )

    musicefy_add_test(testlibraryservice)
    musicefy_add_test(testimageservices)
    musicefy_add_test(testyoutubecore)
    # testsubsonic uses MockHttpClient (tests/mock/MockHttpClient.cpp) to
    # enqueue canned Subsonic JSON by URL substring. Defined inline
    # (mirrors testyoutubeprovider) so the extra .cpp + mock include path
    # are wired in.
    add_executable(testsubsonic
        testsubsonic.cpp
        ${CMAKE_CURRENT_SOURCE_DIR}/mock/MockHttpClient.cpp
    )
    target_link_libraries(testsubsonic PRIVATE musicefy_core Qt5::Test)
    target_include_directories(testsubsonic PRIVATE
        ${CMAKE_SOURCE_DIR}/src/core
        ${CMAKE_SOURCE_DIR}/third_party
        ${CMAKE_CURRENT_SOURCE_DIR}
    )
    musicefy_set_warnings(testsubsonic)
    add_test(NAME testsubsonic COMMAND testsubsonic)
    set_tests_properties(testsubsonic PROPERTIES
        ENVIRONMENT "QT_QPA_PLATFORM=offscreen"
        TIMEOUT 30
    )
    add_custom_command(TARGET testsubsonic POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E copy_directory
            ${CMAKE_SOURCE_DIR}/src/core/database/migrations
            $<TARGET_FILE_DIR:testsubsonic>/migrations
    )
    # testyoutubeprovider needs the MockHttpClient from tests/mock/.
    # We define the executable inline (rather than via
    # musicefy_add_test) so we can include the extra .cpp + the mock
    # include path.
    add_executable(testyoutubeprovider
        testyoutubeprovider.cpp
        ${CMAKE_CURRENT_SOURCE_DIR}/mock/MockHttpClient.cpp
    )
    target_link_libraries(testyoutubeprovider PRIVATE musicefy_core Qt5::Test)
    target_include_directories(testyoutubeprovider PRIVATE
        ${CMAKE_SOURCE_DIR}/src/core
        ${CMAKE_SOURCE_DIR}/third_party
        ${CMAKE_CURRENT_SOURCE_DIR}
    )
    musicefy_set_warnings(testyoutubeprovider)
    add_test(NAME testyoutubeprovider COMMAND testyoutubeprovider)
    set_tests_properties(testyoutubeprovider PROPERTIES
        ENVIRONMENT "QT_QPA_PLATFORM=offscreen"
        TIMEOUT 30
    )
    add_custom_command(TARGET testyoutubeprovider POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E copy_directory
            ${CMAKE_SOURCE_DIR}/src/core/database/migrations
            $<TARGET_FILE_DIR:testyoutubeprovider>/migrations
    )
    musicefy_add_app_test(testcoverimage)
    musicefy_add_app_test(testsvgicon)
    musicefy_add_app_test(testoverlayanimator)
    musicefy_add_test(testsettingscontrol)
    musicefy_add_test(testsmtccontroller)
    musicefy_add_test(testsleeptimer)
    musicefy_add_test(testbotdetection)

    # PlaylistPickerDialog test — compiles the widget directly + LibraryViewModel
    add_executable(testplaylistpickerdialog
        testplaylistpickerdialog.cpp
        ${CMAKE_SOURCE_DIR}/src/app/widgets/PlaylistPickerDialog.cpp
        ${CMAKE_SOURCE_DIR}/src/app/viewmodels/LibraryViewModel.cpp
    )
    target_link_libraries(testplaylistpickerdialog PRIVATE
        musicefy_core musicefy_app Qt5::Test
    )
    target_include_directories(testplaylistpickerdialog PRIVATE
        ${CMAKE_SOURCE_DIR}/src/app
        ${CMAKE_SOURCE_DIR}/src/core
        ${CMAKE_SOURCE_DIR}/third_party
    )
    musicefy_set_warnings(testplaylistpickerdialog)
    add_test(NAME testplaylistpickerdialog COMMAND testplaylistpickerdialog)
    set_tests_properties(testplaylistpickerdialog PROPERTIES
        ENVIRONMENT "QT_QPA_PLATFORM=offscreen"
        TIMEOUT 30
    )
    add_custom_command(TARGET testplaylistpickerdialog POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E copy_directory
            ${CMAKE_SOURCE_DIR}/src/core/database/migrations
            $<TARGET_FILE_DIR:testplaylistpickerdialog>/migrations
    )

    # CardColorGlowBehavior test — widget-only, compiles the behavior directly
    add_executable(testcardcolorglowbehavior
        testcardcolorglowbehavior.cpp
        ${CMAKE_SOURCE_DIR}/src/app/widgets/CardColorGlowBehavior.cpp
    )
    target_link_libraries(testcardcolorglowbehavior PRIVATE
        musicefy_core Qt5::Test
    )
    target_include_directories(testcardcolorglowbehavior PRIVATE
        ${CMAKE_SOURCE_DIR}/src/app
        ${CMAKE_SOURCE_DIR}/src/core
        ${CMAKE_SOURCE_DIR}/third_party
    )
    musicefy_set_warnings(testcardcolorglowbehavior)
    add_test(NAME testcardcolorglowbehavior COMMAND testcardcolorglowbehavior)
    set_tests_properties(testcardcolorglowbehavior PROPERTIES
        ENVIRONMENT "QT_QPA_PLATFORM=offscreen"
        TIMEOUT 30
    )

    # SourceViewModel test — compiles the VM directly, no app graph needed
    add_executable(testsourceviewmodel
        testsourceviewmodel.cpp
        ${CMAKE_SOURCE_DIR}/src/app/viewmodels/SourceViewModel.cpp
    )
    target_link_libraries(testsourceviewmodel PRIVATE
        musicefy_core Qt5::Test
    )
    target_include_directories(testsourceviewmodel PRIVATE
        ${CMAKE_SOURCE_DIR}/src/app
        ${CMAKE_SOURCE_DIR}/src/core
        ${CMAKE_SOURCE_DIR}/third_party
    )
    musicefy_set_warnings(testsourceviewmodel)
    add_test(NAME testsourceviewmodel COMMAND testsourceviewmodel)
    set_tests_properties(testsourceviewmodel PROPERTIES
        ENVIRONMENT "QT_QPA_PLATFORM=offscreen"
        TIMEOUT 30
    )

    add_executable(testsearchviewmodel
        testsearchviewmodel.cpp
    )
    target_link_libraries(testsearchviewmodel PRIVATE
        musicefy_core
        Qt5::Test
    )
    target_include_directories(testsearchviewmodel PRIVATE
        ${CMAKE_SOURCE_DIR}/src/app
        ${CMAKE_SOURCE_DIR}/src/core
        ${CMAKE_SOURCE_DIR}/third_party
    )
    musicefy_set_warnings(testsearchviewmodel)
    add_test(NAME testsearchviewmodel COMMAND testsearchviewmodel)
    set_tests_properties(testsearchviewmodel PROPERTIES
        ENVIRONMENT "QT_QPA_PLATFORM=offscreen"
        TIMEOUT 30
    )

    add_executable(testhomeviewmodel
        testhomeviewmodel.cpp
        ${CMAKE_SOURCE_DIR}/src/app/viewmodels/HomeViewModel.cpp
    )
    target_link_libraries(testhomeviewmodel PRIVATE
        musicefy_core
        Qt5::Test
    )
    target_include_directories(testhomeviewmodel PRIVATE
        ${CMAKE_SOURCE_DIR}/src/app
        ${CMAKE_SOURCE_DIR}/src/core
        ${CMAKE_SOURCE_DIR}/third_party
    )
    musicefy_set_warnings(testhomeviewmodel)
    add_test(NAME testhomeviewmodel COMMAND testhomeviewmodel)
    set_tests_properties(testhomeviewmodel PROPERTIES
        ENVIRONMENT "QT_QPA_PLATFORM=offscreen"
        TIMEOUT 30
    )
    add_custom_command(TARGET testhomeviewmodel POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E copy_directory
            ${CMAKE_SOURCE_DIR}/src/core/database/migrations
            $<TARGET_FILE_DIR:testhomeviewmodel>/migrations
    )

    add_executable(testartistviewmodel
        testartistviewmodel.cpp
        ${CMAKE_SOURCE_DIR}/src/app/viewmodels/ArtistViewModel.cpp
    )
    target_link_libraries(testartistviewmodel PRIVATE
        musicefy_core
        Qt5::Test
    )
    target_include_directories(testartistviewmodel PRIVATE
        ${CMAKE_SOURCE_DIR}/src/app
        ${CMAKE_SOURCE_DIR}/src/core
        ${CMAKE_SOURCE_DIR}/third_party
    )
    musicefy_set_warnings(testartistviewmodel)
    add_test(NAME testartistviewmodel COMMAND testartistviewmodel)
    set_tests_properties(testartistviewmodel PROPERTIES
        ENVIRONMENT "QT_QPA_PLATFORM=offscreen"
        TIMEOUT 30
    )
    add_custom_command(TARGET testartistviewmodel POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E copy_directory
            ${CMAKE_SOURCE_DIR}/src/core/database/migrations
            $<TARGET_FILE_DIR:testartistviewmodel>/migrations
    )

    add_executable(testdiscoverviewmodel
        testdiscoverviewmodel.cpp
        ${CMAKE_SOURCE_DIR}/src/app/viewmodels/DiscoverViewModel.cpp
    )
    target_link_libraries(testdiscoverviewmodel PRIVATE
        musicefy_core
        Qt5::Test
    )
    target_include_directories(testdiscoverviewmodel PRIVATE
        ${CMAKE_SOURCE_DIR}/src/app
        ${CMAKE_SOURCE_DIR}/src/core
        ${CMAKE_SOURCE_DIR}/third_party
    )
    musicefy_set_warnings(testdiscoverviewmodel)
    add_test(NAME testdiscoverviewmodel COMMAND testdiscoverviewmodel)
    set_tests_properties(testdiscoverviewmodel PROPERTIES
        ENVIRONMENT "QT_QPA_PLATFORM=offscreen"
        TIMEOUT 30
    )
    add_custom_command(TARGET testdiscoverviewmodel POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E copy_directory
            ${CMAKE_SOURCE_DIR}/src/core/database/migrations
            $<TARGET_FILE_DIR:testdiscoverviewmodel>/migrations
    )
endif()
