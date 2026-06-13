// JsCipherExtractor.cpp — see JsCipherExtractor.h.

#include "JsCipherExtractor.h"

#include <QRegularExpression>
#include <QSet>
#include <QStringList>

namespace mf::core::sources::youtube {

bool JsCipherExtractor::isValidOpsTable(const QString& ops) {
    if (ops.isEmpty()) return false;
    const QStringList parts = ops.split(QLatin1Char(','));
    if (parts.isEmpty()) return false;
    static const QSet<QString> kValid = {QStringLiteral("1"),
                                          QStringLiteral("2"),
                                          QStringLiteral("3"),
                                          QStringLiteral("4")};
    for (const QString& p : parts) {
        const QString t = p.trimmed();
        if (t.isEmpty()) return false;
        if (!kValid.contains(t)) return false;
    }
    return true;
}

namespace {

// Pattern 1: a string literal that looks like an ops table is
// assigned to a variable, and we can find a `.split("")` call on
// either the literal itself (less common) or on the variable
// (common in 2024-2025 builds). We anchor on the `.split("")`
// because that's the dead-giveaway for the signature cipher.
//
//   var Nf = "1,28,2,3,3,1,49,4";
//   ...
//   Nf.split("");
//
//   or
//
//   obj.split("1,28,2,3,3,1,49,4");
QString findBySplitWindow(const QString& js, int window = 1500) {
    static const QRegularExpression splitRe(
        QStringLiteral(R"(\.split\(""\))"));
    QRegularExpressionMatch m = splitRe.match(js);
    if (!m.hasMatch()) return QString();

    // Walk backward from the .split("") match looking for an ops
    // string. We look up to `window` characters back to allow for
    // minified wrapping.
    int back = qMax(0, m.capturedStart() - window);
    QString prefix = js.mid(back, m.capturedStart() - back);

    static const QRegularExpression opsRe(
        QStringLiteral(R"((["'])([0-9]+(?:\s*,\s*[0-9]+){2,})\1)"));
    QRegularExpressionMatchIterator it = opsRe.globalMatch(prefix);
    QString best;
    while (it.hasNext()) {
        QRegularExpressionMatch mm = it.next();
        const QString ops = mm.captured(2);
        // Normalise whitespace.
        QString compact;
        const QStringList parts = ops.split(QLatin1Char(','));
        for (int i = 0; i < parts.size(); ++i) {
            const QString trimmed = parts[i].trimmed();
            if (i > 0) compact.append(QLatin1Char(','));
            compact.append(trimmed);
        }
        if (JsCipherExtractor::isValidOpsTable(compact)) {
            // Prefer the longest match (the .split("")'s input is
            // typically the last op table encountered in the
            // window). Walk all matches and keep the one closest
            // to the .split("") call.
            if (best.isEmpty() || mm.capturedStart() > prefix.lastIndexOf(best)) {
                best = compact;
            }
        }
    }
    return best;
}

// Pattern fallback: if the .split("") is in the JS but no ops
// string is within the back-window, look forward a small distance
// for a variable definition `var <id> = "1,28,..."` and assume
// the .split("") targets that variable. This catches builds where
// the var is defined AFTER the split call (rare but happens in
// minified output).
QString findByForwardVar(const QString& js, int window = 2000) {
    static const QRegularExpression splitRe(
        QStringLiteral(R"(\.split\(""\))"));
    QRegularExpressionMatch m = splitRe.match(js);
    if (!m.hasMatch()) return QString();

    int fwd = qMin(js.size(), m.capturedEnd() + window);
    QString suffix = js.mid(m.capturedEnd(), fwd - m.capturedEnd());

    static const QRegularExpression varRe(
        QStringLiteral(R"((?:var|let|const)\s+([A-Za-z_$][\w$]*)\s*=\s*["']([0-9]+(?:\s*,\s*[0-9]+){2,})["'])"));
    QRegularExpressionMatchIterator it = varRe.globalMatch(suffix);
    while (it.hasNext()) {
        QRegularExpressionMatch mm = it.next();
        const QString ops = mm.captured(2);
        QString compact;
        const QStringList parts = ops.split(QLatin1Char(','));
        for (int i = 0; i < parts.size(); ++i) {
            const QString trimmed = parts[i].trimmed();
            if (i > 0) compact.append(QLatin1Char(','));
            compact.append(trimmed);
        }
        if (JsCipherExtractor::isValidOpsTable(compact)) {
            return compact;
        }
    }
    return QString();
}

} // namespace

QString JsCipherExtractor::extractFromJs(const QString& jsSource) {
    if (jsSource.isEmpty()) return QString();

    // Pattern 1a: the ops string is in the .split("") vicinity.
    QString result = findBySplitWindow(jsSource);
    if (!result.isEmpty()) return result;

    // Pattern 1b: look forward from the .split("") for a var.
    result = findByForwardVar(jsSource);
    if (!result.isEmpty()) return result;

    // Pattern 2 (known gap): the ops string is at an unrelated
    // location in the JS. Surface as failure.
    return QString();
}

} // namespace mf::core::sources::youtube
