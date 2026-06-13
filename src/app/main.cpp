// main.cpp
// Application entry point. Sets up QApplication, builds the
// AppContainer (DI graph), starts the AppLifecycle, and shows the
// main window. Returns the Qt event loop's exit code.
//
// Command-line flags:
//   --no-window     run headless (no MainWindow shown). Useful for
//                   CI smoke tests that just want to exercise the
//                   container graph.
//   --exit-after=N  exit cleanly after N seconds. Used by smoke tests.

#include "AppContainer.h"
#include "AppLifecycle.h"
#include "MainWindow.h"
#include "widgets/SplashScreen.h"

#include <QApplication>
#include <QCommandLineParser>
#include <QMessageBox>
#include <QScreen>
#include <QTimer>
#include <exception>

#if defined(MUSICEFY_ENABLE_WINRT_SRTC) && defined(Q_OS_WIN)
#  include <winrt/base.h>
#endif

int main(int argc, char* argv[]) {
#if defined(MUSICEFY_ENABLE_WINRT_SRTC) && defined(Q_OS_WIN)
    winrt::init_apartment(winrt::apartment_type::multi_threaded);
#endif

    QApplication app(argc, argv);
    QCoreApplication::setOrganizationName(QStringLiteral("Musicefy"));
    QCoreApplication::setOrganizationDomain(QStringLiteral("musicefy.local"));
    QCoreApplication::setApplicationName(QStringLiteral("Musicefy"));
    QCoreApplication::setApplicationVersion(QStringLiteral("2.0.0"));

    QCommandLineParser parser;
    parser.setApplicationDescription(QStringLiteral("Musicefy \u2014 a modern music player for Windows 7+"));
    parser.addHelpOption();
    parser.addVersionOption();
    QCommandLineOption noWindow(QStringList{"no-window"},
        QStringLiteral("Run headless (no main window)."));
    QCommandLineOption exitAfter(QStringList{"exit-after"},
        QStringLiteral("Exit after N seconds."), QStringLiteral("seconds"));
    parser.addOption(noWindow);
    parser.addOption(exitAfter);
    parser.process(app);

    // ── Global Error Handler ─────────────────────────────────────────
    // Route all Qt messages to stderr so they survive the splash screen
    // disappearing and are visible in the console / CI logs.
    qInstallMessageHandler([](QtMsgType type, const QMessageLogContext& ctx,
                              const QString& msg) {
        Q_UNUSED(ctx);
        auto* output = qPrintable(msg);
        switch (type) {
            case QtDebugMsg:    fprintf(stderr, "[DEBUG] %s\n", output); break;
            case QtInfoMsg:     fprintf(stderr, "[INFO]  %s\n", output); break;
            case QtWarningMsg:  fprintf(stderr, "[WARN]  %s\n", output); break;
            case QtCriticalMsg: fprintf(stderr, "[ERROR] %s\n", output); break;
            case QtFatalMsg:    fprintf(stderr, "[FATAL] %s\n", output); break;
        }
    });

    // ── Splash Screen ──────────────────────────────────────────────
    mf::app::widgets::SplashScreen* splash = nullptr;
    if (!parser.isSet(noWindow)) {
        QPixmap logo(QStringLiteral(":/musicefy.png"));
        if (logo.isNull()) {
            // Fallback: try loading from the app directory.
            logo.load(QCoreApplication::applicationDirPath()
                      + QStringLiteral("/musicefy.png"));
        }
        splash = new mf::app::widgets::SplashScreen(logo);

        // Center the splash screen on the primary display.
        if (QScreen* screen = QApplication::primaryScreen()) {
            QRect screenGeo = screen->availableGeometry();
            splash->move((screenGeo.width()  - splash->width())  / 2 + screenGeo.x(),
                         (screenGeo.height() - splash->height()) / 2 + screenGeo.y());
        }

        splash->show();
        splash->startAnimation();
        splash->setProgress(10);
        splash->setMessage(QStringLiteral("Initializing\u2026"));
        app.processEvents();
    }

    // ── Container Build + Lifecycle ────────────────────────────────
    int exitCode = 1;
    try {
        if (splash) {
            splash->setProgress(20);
            splash->setMessage(QStringLiteral("Building services\u2026"));
            app.processEvents();
        }

        mf::app::AppContainer container;
        container.build();

        if (splash) {
            splash->setProgress(60);
            splash->setMessage(QStringLiteral("Starting services\u2026"));
            app.processEvents();
        }

        mf::app::AppLifecycle lifecycle(container);
        lifecycle.start();

        if (splash) {
            splash->setProgress(90);
            splash->setMessage(QStringLiteral("Loading interface\u2026"));
            app.processEvents();
        }

        exitCode = 0;
        if (parser.isSet(noWindow)) {
            if (parser.isSet(exitAfter)) {
                bool ok;
                int secs = parser.value(exitAfter).toInt(&ok);
                if (ok && secs > 0) {
                    QTimer::singleShot(secs * 1000, &app, [&]() {
                        lifecycle.shutdown();
                        app.quit();
                    });
                }
            }
            if (splash) {
                splash->stopAnimation();
                splash->close();
                delete splash;
                splash = nullptr;
            }
            exitCode = app.exec();
        } else {
            if (splash) {
                splash->setProgress(100);
                splash->setMessage(QStringLiteral("Ready"));
                app.processEvents();
            }

            mf::app::MainWindow window(container);
            window.show();

            if (splash) {
                splash->stopAnimation();
                splash->finish(&window);
                delete splash;
                splash = nullptr;
            }

            exitCode = app.exec();
        }

        lifecycle.shutdown();

    } catch (const std::exception& ex) {
        // Clean up the splash screen so the error dialog is visible.
        if (splash) {
            splash->stopAnimation();
            splash->close();
            delete splash;
            splash = nullptr;
        }
        QMessageBox::critical(nullptr,
            QStringLiteral("Musicefy \u2014 Error"),
            QStringLiteral("An error occurred during startup:\n\n%1")
                .arg(QString::fromUtf8(ex.what())));
        exitCode = 1;
    } catch (...) {
        if (splash) {
            splash->stopAnimation();
            splash->close();
            delete splash;
            splash = nullptr;
        }
        QMessageBox::critical(nullptr,
            QStringLiteral("Musicefy \u2014 Error"),
            QStringLiteral("An unknown error occurred during startup.\n\n"
                           "Check the debug console for details."));
        exitCode = 1;
    }

#if defined(MUSICEFY_ENABLE_WINRT_SRTC) && defined(Q_OS_WIN)
    winrt::uninit_apartment();
#endif
    return exitCode;
}
