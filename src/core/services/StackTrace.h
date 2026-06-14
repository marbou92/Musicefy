// StackTrace.h
// Header-only Windows stack trace helper using DbgHelp.
// On non-Windows platforms, falls back to a placeholder message.

#pragma once

#include <QString>

#ifdef Q_OS_WIN

#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#include <windows.h>
#include <dbghelp.h>
#include <cstdio>

namespace mf::core::services {

/// Capture a stack trace from the current thread and format it as a
/// human-readable multi-line string. Returns "Stack trace unavailable"
/// if symbols cannot be resolved.
inline QString captureStackTrace(int maxFrames = 64) {
    // DbgHelp is not thread-safe — use a static flag for one-time init.
    static bool symbolsReady = false;
    if (!symbolsReady) {
        SymSetOptions(SYMOPT_DEFERRED_LOADS | SYMOPT_UNDNAME);
        HANDLE process = GetCurrentProcess();
        SymInitialize(process, NULL, TRUE);
        symbolsReady = true;
    }

    void* frames[64] = {};
    USHORT count = CaptureStackBackTrace(1, maxFrames, frames, NULL);

    if (count == 0) {
        return QStringLiteral("(empty stack trace)\n");
    }

    HANDLE process = GetCurrentProcess();
    HANDLE thread  = GetCurrentThread();

    QString result;
    char buffer[sizeof(SYMBOL_INFO) + 256];
    SYMBOL_INFO* symbol = reinterpret_cast<SYMBOL_INFO*>(buffer);
    symbol->MaxNameLen   = 255;
    symbol->SizeOfStruct = sizeof(SYMBOL_INFO);

    IMAGEHLP_LINE64 line = {};
    line.SizeOfStruct = sizeof(IMAGEHLP_LINE64);

    for (USHORT i = 0; i < count; ++i) {
        // Resolve symbol name.
        QString frameText;
        if (SymFromAddr(process, reinterpret_cast<DWORD64>(frames[i]), 0, symbol)) {
            frameText = QString::fromLatin1(symbol->Name);
        } else {
            frameText = QStringLiteral("(unknown)");
        }

        // Resolve source file + line number (best effort).
        DWORD displacement = 0;
        if (SymGetLineFromAddr64(process, reinterpret_cast<DWORD64>(frames[i]),
                                 &displacement, &line)) {
            result += QStringLiteral("  [%1] %2 (%3:%4)\n")
                          .arg(i)
                          .arg(frameText)
                          .arg(QString::fromLatin1(line.FileName))
                          .arg(line.LineNumber);
        } else {
            result += QStringLiteral("  [%1] %2\n")
                          .arg(i)
                          .arg(frameText);
        }
    }

    return result;
}

} // namespace mf::core::services

#else // !Q_OS_WIN

namespace mf::core::services {

inline QString captureStackTrace(int /*maxFrames*/ = 64) {
    return QStringLiteral("(stack trace not supported on this platform)\n");
}

} // namespace mf::core::services

#endif
