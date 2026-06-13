// Cipher.cpp — see Cipher.h.

#include "Cipher.h"

#include <QChar>
#include <QStringList>

#include <algorithm>
#include <utility>

namespace mf::core::sources::youtube {

std::vector<Cipher::Operation> Cipher::parseOperations(const QString& compact) {
    std::vector<Operation> out;
    if (compact.isEmpty()) return out;

    // The compact form is a comma-separated sequence of integers. Each
    // operation is 1-3 ints depending on kind:
    //   <op>           → Reverse
    //   <op>, <a>      → Swap / Slice
    //   <op>, <a>, <b> → Splice
    //
    // Walk the integer stream left to right, dispatching on the first
    // int (op-code).
    const QStringList tokens = compact.split(QLatin1Char(','), Qt::SkipEmptyParts);
    out.reserve(tokens.size() / 2);

    int i = 0;
    while (i < tokens.size()) {
        bool ok = false;
        const int op = tokens[i].toInt(&ok);
        if (!ok) {
            // Bail out on malformed input — caller is expected to
            // detect the empty result and treat it as "cipher
            // unsupported, fall back to yt-dlp".
            return {};
        }
        switch (op) {
        case 1: {
            if (i + 1 >= tokens.size()) return {};
            Operation o; o.kind = Op::Swap; o.a = tokens[i + 1].toInt();
            out.push_back(o);
            i += 2;
            break;
        }
        case 2: {
            if (i + 1 >= tokens.size()) return {};
            Operation o; o.kind = Op::Slice; o.a = tokens[i + 1].toInt();
            out.push_back(o);
            i += 2;
            break;
        }
        case 3: {
            if (i + 2 >= tokens.size()) return {};
            Operation o; o.kind = Op::Splice; o.a = tokens[i + 1].toInt();
            o.b = tokens[i + 2].toInt();
            out.push_back(o);
            i += 3;
            break;
        }
        case 4: {
            Operation o; o.kind = Op::Reverse;
            out.push_back(o);
            i += 1;
            break;
        }
        default:
            // Unknown op — bail. This indicates a newer cipher version
            // the implementation hasn't been updated for; the caller
            // should treat it as "decipher failed" and fall back.
            return {};
        }
    }
    return out;
}

QString Cipher::apply(const QString& signature, const std::vector<Operation>& ops) {
    if (signature.isEmpty() || ops.empty()) return QString();

    // The cipher manipulates QString as a mutable buffer. We work on a
    // QByteArray for O(1) splice / slice semantics and convert back at
    // the end. The signatures are 7-bit ASCII so this round-trip is
    // safe.
    QByteArray buf = signature.toLatin1();

    for (const Operation& op : ops) {
        switch (op.kind) {
        case Op::Swap: {
            const int n = op.a;
            if (n < 0 || n >= buf.size()) return QString();
            { char tmp = buf[0]; buf[0] = buf[n]; buf[n] = tmp; }
            break;
        }
        case Op::Reverse: {
            std::reverse(buf.begin(), buf.end());
            break;
        }
        case Op::Slice: {
            const int n = qMax(0, op.a);
            if (n >= buf.size()) return QString();
            buf = buf.left(n);
            break;
        }
        case Op::Splice: {
            const int n = op.a;
            const int m = op.b;
            if (n < 0 || n >= buf.size() || m < 0) return QString();
            const int len = qMin(m, buf.size() - n);
            buf.remove(n, len);
            break;
        }
        }
    }
    return QString::fromLatin1(buf);
}

QString Cipher::decipher(const QString& signature, const QString& compactOps) {
    if (signature.isEmpty() || compactOps.isEmpty()) return QString();
    const auto ops = parseOperations(compactOps);
    if (ops.empty()) return QString();
    return apply(signature, ops);
}

QString Cipher::nParamDecode(const QString& nParam, const QStringList& decoderTable) {
    // The n-parameter decoder table shipped by the player is an
    // ordered list of (mostly charAt-style) operations:
    //
    //   nfunc[0]  — base n string
    //   nfunc[1+] — string table of constants
    //   then a sequence of integer op-codes:
    //     1 → wrap with char from nfunc[1]
    //     2 → wrap with char from nfunc[2]
    //     3 → reverse
    //     4 → slice(a)
    //     5 → splice(a, b)
    //
    // Most modern (2024-2025) client configurations either don't
    // include an n-param decoder or only use the first wrap op. We
    // implement the common subset; anything more exotic falls through
    // and the caller should fall back to yt-dlp.
    if (nParam.isEmpty() || decoderTable.size() < 3) {
        return nParam;
    }
    QByteArray buf = nParam.toLatin1();

    // We treat decoderTable[0] as the base value and decoderTable[1+]
    // as the wrap chars. Modern builds occasionally embed the op list
    // inline as an additional JSON field; for now we accept a literal
    // op list passed as decoderTable[2+].
    if (decoderTable.size() >= 4) {
        const QString ops = decoderTable.last();
        const auto parsed = parseOperations(ops);
        for (const auto& op : parsed) {
            switch (op.kind) {
            case Op::Swap: {
                if (op.a >= 0 && op.a < buf.size()) { char tmp = buf[0]; buf[0] = buf[op.a]; buf[op.a] = tmp; }
                break;
            }
            case Op::Reverse: std::reverse(buf.begin(), buf.end()); break;
            case Op::Slice:
                if (op.a >= 0 && op.a < buf.size()) buf = buf.left(op.a);
                break;
            case Op::Splice: {
                if (op.a >= 0 && op.a < buf.size() && op.b >= 0) {
                    buf.remove(op.a, qMin(op.b, buf.size() - op.a));
                }
                break;
            }
            }
        }
    }
    return QString::fromLatin1(buf);
}

} // namespace mf::core::sources::youtube