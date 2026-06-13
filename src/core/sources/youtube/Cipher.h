// Cipher.h
// YouTube signature-cipher (a.k.a. "s=" parameter) deobfuscation.
//
// YouTube returns streaming URLs whose signature parameter `s` is a
// scrambled string. The player's "signatureCipher" blob (`sc` array +
// `sp` signature parameter) describes a fixed sequence of string
// operations that, when applied to the scrambled string, yields the
// correct signature. The set of operations is small:
//
//   swap(N)         — swap the char at position N with the char at pos 0
//   reverse()       — reverse the entire string
//   slice(N)        — take the first N chars
//   splice(N, M)    — remove M chars starting at position N
//
// This file implements the parser (compact form: e.g. "45,28,3,2,1,49"
// as found in the player JS) and the runner. Direct port of the cipher
// logic embedded in YoutubeExplode and the well-known
// YouTubeExplode.Converter / yt-dlp_sig.js routines.
//
// This is purely deterministic — no network, no Qt GUI, fully unit-testable.

#pragma once

#include <QString>
#include <QStringList>

#include <vector>

namespace mf::core::sources::youtube {

class Cipher {
public:
    // Operation kinds. Order matters in the JSON decoder array.
    enum class Op {
        Swap,
        Reverse,
        Slice,
        Splice,
    };

    struct Operation {
        Op  kind;
        int a = 0; // for Swap/Slice/Splice
        int b = 0; // for Splice
    };

    // Parse a compact operations string such as "45,28,3,2,1,49" or
    // "wgwIEAAqDgsQ...AFBwgB"; here we accept the 3-operand-per-op
    // comma-separated form which is what the player returns in the
    // "sc" array. Each operation is encoded as:
    //     <opCode>, <argA>, <argB?>
    // where opCode is one of:
    //     1  → Swap(a)
    //     2  → Slice(a)
    //     3  → Splice(a, b)
    //     4  → Reverse()
    static std::vector<Operation> parseOperations(const QString& compact);

    // Apply the decoded operations to a signature. The signature comes
    // straight from the URL's `s=` query parameter. The result is the
    // value the client should send back in `sig=`.
    static QString apply(const QString& signature,
                         const std::vector<Operation>& ops);

    // Convenience: parse + apply in one shot. Returns an empty string
    // on failure (parse or apply error).
    static QString decipher(const QString& signature, const QString& compactOps);

    // Decode the n-param (only the "n" query parameter requires
    // splitting when present; many clients skip it). The n-param
    // decoder table is typically an array of char operations that we
    // re-implement here. The split-position function is
    // n-parameter-decoder:
    //   • nfunc[0] = <funcName>
    //   • nfunc[1..] = string-table of constants
    //   • func body performs slice / reverse / charAt composition
    // We provide a simpler high-level helper: nParamDecode(n, table)
    // that handles the most common cases observed in 2024-2025 player
    // builds. Returns the input unchanged if decoding isn't applicable
    // (no decoder table, n already valid, etc.).
    static QString nParamDecode(const QString& nParam, const QStringList& decoderTable);
};

} // namespace mf::core::sources::youtube
