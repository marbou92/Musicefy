// JsCipherExtractor.h
// Regex-based extraction of the YouTube player's signature-cipher
// operations string (the `sc` array, e.g. "1,28,2,3,3,1,49,4").
//
// Pure function: no network, no I/O. Given the raw JavaScript
// source, returns the ops string or empty on failure.
//
// The extraction handles pattern 1 (the most common in 2024-2025
// player builds):
//
//   var <ident> = "<comma-separated-ops>";
//   …
//   <ident>.split("");
//
// or sometimes:
//
//   <obj>.split("<comma-separated-ops>");
//
// Pattern 2 — where the ops string lives at a separate literal
// and is referenced by name — is a known gap; we surface it as a
// parse failure so the session can fall back to yt-dlp (Block
// 5.2.C).

#pragma once

#include <QString>

namespace mf::core::sources::youtube {

class JsCipherExtractor {
public:
    // Returns the compact ops string (e.g. "1,28,2,3,3,1,49,4"),
    // or an empty string if no recognisable table was found.
    //
    // The returned string is guaranteed to be a comma-separated list
    // of integers in {1,2,3,4} (the four Cipher operations: swap,
    // slice, splice, reverse). Anything else is treated as a parse
    // failure and returns an empty string.
    static QString extractFromJs(const QString& jsSource);

    // True if `ops` looks like a valid ops table (each token in
    // {1,2,3,4}, non-empty, comma-separated). Exposed for tests.
    static bool isValidOpsTable(const QString& ops);
};

} // namespace mf::core::sources::youtube
