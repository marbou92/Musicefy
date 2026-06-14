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
//
// Crash / error logging:
//   All errors, warnings, and fatal crashes are logged to:
//     %LOCALAPPDATA%/Musicefy/error.log
//   Hard crashes (SIGSEGV, etc.) are caught by platform signal handlers
//   that write a stack trace to the same file before aborting.

#include "AppContainer.h"
#include "AppLifecycle.h"
#include "MainWindow.h"
#include "widgets/SplashScreen.h"
#include "../core/services/CrashLogger.h"
#include "../core/services/StackTrace.h"

#include <QApplication>
#include <QCommandLineParser>
#include <QMessageBox>
#include <QScreen>
#include <QTimer>
#include <exception>

#include <csignal>

#ifdef Q_OS_WIN
#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#include <windows.h>   // SetUnhandledExceptionFilter, EXCEPTION_POINTERS
#endif

#if defined(MUSICEFY_ENABLE_WINRT_SRTC) && defined(Q_OS_WIN)
#  include <winrt/base.h>
#endif

// ── Crash / Signal Handlers ───────────────────────────────────────────────
//
// These run outside the Qt event loop, so they only use async-signal-safe
// or mutex-protected calls. The goal is to write a stack trace to
// error.log before the process terminates.

namespace {

void signalHandler(int sig) {
    auto& log = mf::core::services::CrashLogger::instance();

    const char* name = "UNKNOWN";
    switch (sig) {
        case SIGSEGV: name = "SIGSEGV (segmentation fault)"; break;
        case SIGABRT: name = "SIGABRT (abort)";              break;
        case SIGFPE:  name = "SIGFPE (floating-point error)"; break;
        case SIGILL:  name = "SIGILL (illegal instruction)";  break;
#ifdef Q_OS_WIN
        case SIGBREAK: name = "SIGBREAK (Ctrl+Break)";       break;
#endif
    }

    log.writeSignal(name);
    log.write(mf::core::services::captureStackTrace());
    log.flush();

    // Re-raise with default handler so the OS generates a crash dump.
    std::signal(sig, SIG_DFL);
    std::raise(sig);
}

#ifdef Q_OS_WIN
LONG WINAPI sehHandler(EXCEPTION_POINTERS* info) {
    auto& log = mf::core::services::CrashLogger::instance();

    const char* name = "UNKNOWN";
    if (info && info->ExceptionRecord) {
        switch (info->ExceptionRecord->ExceptionCode) {
            case EXCEPTION_ACCESS_VIOLATION:     name = "ACCESS_VIOLATION (0xC0000005)"; break;
            case EXCEPTION_STACK_OVERFLOW:       name = "STACK_OVERFLOW (0xC00000FD)";   break;
            case EXCEPTION_INT_DIVIDE_BY_ZERO:   name = "INT_DIVIDE_BY_ZERO";           break;
            case EXCEPTION_FLT_DIVIDE_BY_ZERO:   name = "FLT_DIVIDE_BY_ZERO";           break;
            case EXCEPTION_ILLEGAL_INSTRUCTION:  name = "ILLEGAL_INSTRUCTION";          break;
            case EXCEPTION_INVALID_DISPOSITION:  name = "INVALID_DISPOSITION";          break;
            case EXCEPTION_GUARD_PAGE:           name = "GUARD_PAGE";                   break;
        }
    }

    log.writeSignal(name);
    log.write(mf::core::services::captureStackTrace());
    log.flush();

    return EXCEPTION_EXECUTE_HANDLER;
}
#endif

void installCrashHandlers() {
    // POSIX signals
    std::signal(SIGSEGV, signalHandler);
    std::signal(SIGABRT, signalHandler);
    std::signal(SIGFPE,  signalHandler);
    std::signal(SIGILL,  signalHandler);
#ifdef Q_OS_WIN
    std::signal(SIGBREAK, signalHandler);
    // Windows SEH for access violations, stack overflows, etc.
    SetUnhandledExceptionFilter(sehHandler);
#endif
}

} // anonymous namespace

int main(int argc, char* argv[]) {
#if defined(MUSICEFY_ENABLE_WINRT_SRTC) && defined(Q_OS_WIN)
    winrt::init_apartment(winrt::apartment_type::multi_threaded);
#endif

    // ── Install crash signal handlers FIRST ─────────────────────────
    // Must be before QApplication so hard crashes (SIGSEGV etc.) are
    // caught even if the crash happens during Qt initialization.
    installCrashHandlers();
    // Force-initialize the logger + write a session marker.
    mf::core::services::CrashLogger::instance().write(
        QStringLiteral("main() — crash handlers installed, argc=%1").arg(argc));

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
    // Also mirror warnings+ to error.log for crash diagnostics.
    qInstallMessageHandler([](QtMsgType type, const QMessageLogContext& ctx,
                              const QString& msg) {
        Q_UNUSED(ctx);
        const char* prefix = "";
        auto* output = qPrintable(msg);
        switch (type) {
            case QtDebugMsg:    fprintf(stderr, "[DEBUG] %s\n", output); prefix = "DEBUG"; break;
            case QtInfoMsg:     fprintf(stderr, "[INFO]  %s\n", output); prefix = "INFO";  break;
            case QtWarningMsg:  fprintf(stderr, "[WARN]  %s\n", output); prefix = "WARN";  break;
            case QtCriticalMsg: fprintf(stderr, "[ERROR] %s\n", output); prefix = "ERROR"; break;
            case QtFatalMsg:    fprintf(stderr, "[FATAL] %s\n", output); prefix = "FATAL"; break;
        }
        // Mirror info, warnings, errors, and fatals to the persistent log file.
        if (type >= QtInfoMsg) {
            mf::core::services::CrashLogger::instance().write(
                QStringLiteral("[%1] %2").arg(QString::fromLatin1(prefix)).arg(msg));
        }
        if (type == QtFatalMsg) {
            mf::core::services::CrashLogger::instance().flush();
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
            splash->stopAnimation();
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
        // Log the exception with full stack trace before showing UI.
        mf::core::services::CrashLogger::instance().writeException(ex);
        mf::core::services::CrashLogger::instance().write(
            mf::core::services::captureStackTrace());
        mf::core::services::CrashLogger::instance().flush();

        // Clean up the splash screen so the error dialog is visible.
        if (splash) {
            splash->stopAnimation();
            splash->close();
            delete splash;
            splash = nullptr;
        }

        const QString logPath = mf::core::services::CrashLogger::instance().logFilePath();
        const QString fbPath  = mf::core::services::CrashLogger::instance().fallbackLogPath();
        QMessageBox::critical(nullptr,
            QStringLiteral("Musicefy \u2014 Error"),
            QStringLiteral("An error occurred during startup:\n\n%1\n\n"
                           "Details have been logged to:\n%2\nAlso: %3")
                .arg(QString::fromUtf8(ex.what()))
                .arg(logPath)
                .arg(fbPath));
        exitCode = 1;
    } catch (...) {
        mf::core::services::CrashLogger::instance().write(
            QStringLiteral("Unknown exception (catch ...)"));
        mf::core::services::CrashLogger::instance().write(
            mf::core::services::captureStackTrace());
        mf::core::services::CrashLogger::instance().flush();

        if (splash) {
            splash->stopAnimation();
            splash->close();
            delete splash;
            splash = nullptr;
        }

        const QString logPath = mf::core::services::CrashLogger::instance().logFilePath();
        const QString fbPath  = mf::core::services::CrashLogger::instance().fallbackLogPath();
        QMessageBox::critical(nullptr,
            QStringLiteral("Musicefy \u2014 Error"),
            QStringLiteral("An unknown error occurred during startup.\n\n"
                           "Details have been logged to:\n%1\nAlso: %2")
                .arg(logPath)
                .arg(fbPath));
        exitCode = 1;
    }

#if defined(MUSICEFY_ENABLE_WINRT_SRTC) && defined(Q_OS_WIN)
    winrt::uninit_apartment();
#endif
    return exitCode;
}
