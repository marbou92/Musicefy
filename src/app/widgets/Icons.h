// Icons.h
// Lucide icon set (MIT-licensed, https://lucide.dev). Each icon is
// a 24x24 viewBox stroke-based glyph. Use with SvgIcon::get(name,
// color, size) to render at a given size in a given color.
//
// Add new icons by appending to kLucideIcons. Names follow the
// Lucide naming convention (kebab-case becomes the map key).

#pragma once

#include <QHash>
#include <QString>

namespace mf::app::widgets::icons {

inline const QHash<QString, QString>& all() {
    static const QHash<QString, QString> kMap = {
        // ── Sidebar (5) ─────────────────────────────────────────────
        { "home",       "<path d=\"m3 9 9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z\"/>"
                        "<polyline points=\"9 22 9 12 15 12 15 22\"/>" },
        { "search",     "<circle cx=\"11\" cy=\"11\" r=\"8\"/>"
                        "<line x1=\"21\" y1=\"21\" x2=\"16.65\" y2=\"16.65\"/>" },
        { "library",    "<path d=\"m16 6 4 14\"/>"
                        "<path d=\"M12 6v14\"/>"
                        "<path d=\"M8 8v12\"/>"
                        "<path d=\"M4 4v16\"/>" },
        { "settings",   "<circle cx=\"12\" cy=\"12\" r=\"3\"/>"
                        "<path d=\"M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1 0 2.83 2 2 0 0 1-2.83 0l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83 0 2 2 0 0 1 0-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 0-2.83 2 2 0 0 1 2.83 0l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 0 2 2 0 0 1 0 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z\"/>" },
        { "sparkles",   "<path d=\"M9.937 15.5A2 2 0 0 0 8.5 14.063l-6.135-1.582a.5.5 0 0 1 0-.962L8.5 9.936A2 2 0 0 0 9.937 8.5l1.582-6.135a.5.5 0 0 1 .963 0L14.063 8.5A2 2 0 0 0 15.5 9.937l6.135 1.581a.5.5 0 0 1 0 .964L15.5 14.063a2 2 0 0 0-1.437 1.437l-1.582 6.135a.5.5 0 0 1-.963 0z\"/>"
                        "<path d=\"M20 3v4\"/><path d=\"M22 5h-4\"/>"
                        "<path d=\"M4 17v2\"/><path d=\"M5 18H3\"/>" },

        // ── Transport (4) ──────────────────────────────────────────
        { "play",        "<polygon points=\"6 3 20 12 6 21 6 3\"/>" },
        { "pause",       "<rect x=\"6\"  y=\"4\" width=\"4\" height=\"16\" rx=\"1\"/>"
                         "<rect x=\"14\" y=\"4\" width=\"4\" height=\"16\" rx=\"1\"/>" },
        { "skip-back",   "<polygon points=\"19 20 9 12 19 4 19 20\"/>"
                         "<line x1=\"5\" y1=\"19\" x2=\"5\" y2=\"5\"/>" },
        { "skip-forward","<polygon points=\"5 4 15 12 5 20 5 4\"/>"
                         "<line x1=\"19\" y1=\"5\" x2=\"19\" y2=\"19\"/>" },

        // ── Common (5) ─────────────────────────────────────────────
        { "arrow-left",  "<line x1=\"19\" y1=\"12\" x2=\"5\" y2=\"12\"/>"
                         "<polyline points=\"12 19 5 12 12 5\"/>" },
        { "shuffle",     "<polyline points=\"16 3 21 3 21 8\"/>"
                         "<line x1=\"4\" y1=\"20\" x2=\"21\" y2=\"3\"/>"
                         "<polyline points=\"21 16 21 21 16 21\"/>"
                         "<line x1=\"15\" y1=\"15\" x2=\"21\" y2=\"21\"/>"
                         "<line x1=\"4\" y1=\"4\" x2=\"9\" y2=\"9\"/>" },
        { "repeat",      "<polyline points=\"17 1 21 5 17 9\"/>"
                         "<path d=\"M3 11V9a4 4 0 0 1 4-4h14\"/>"
                         "<polyline points=\"7 23 3 19 7 15\"/>"
                         "<path d=\"M21 13v2a4 4 0 0 1-4 4H3\"/>" },
        { "volume-2",    "<polygon points=\"11 5 6 9 2 9 2 15 6 15 11 19 11 5\"/>"
                         "<path d=\"M19.07 4.93a10 10 0 0 1 0 14.14\"/>"
                         "<path d=\"M15.54 8.46a5 5 0 0 1 0 7.07\"/>" },
        { "refresh-cw",  "<polyline points=\"23 4 23 10 17 10\"/>"
                         "<polyline points=\"1 20 1 14 7 14\"/>"
                         "<path d=\"M3.51 9a9 9 0 0 1 14.85-3.36L23 10\"/>"
                         "<path d=\"M1 14l4.64 4.36A9 9 0 0 0 20.49 15\"/>" },

        // ── State (4) ──────────────────────────────────────────────
        { "star",        "<polygon points=\"12 2 15.09 8.26 22 9.27 17 14.14 18.18 21.02 12 17.77 5.82 21.02 7 14.14 2 9.27 8.91 8.26 12 2\"/>" },
        { "plus",        "<line x1=\"12\" y1=\"5\" x2=\"12\" y2=\"19\"/>"
                         "<line x1=\"5\"  y1=\"12\" x2=\"19\" y2=\"12\"/>" },
        { "heart",       "<path d=\"M19 14c1.49-1.46 3-3.21 3-5.5A5.5 5.5 0 0 0 16.5 3c-1.76 0-3 .5-4.5 2-1.5-1.5-2.74-2-4.5-2A5.5 5.5 0 0 0 2 8.5c0 2.3 1.5 4.05 3 5.5l7 7Z\"/>" },
        { "heart-filled","<path d=\"M19 14c1.49-1.46 3-3.21 3-5.5A5.5 5.5 0 0 0 16.5 3c-1.76 0-3 .5-4.5 2-1.5-1.5-2.74-2-4.5-2A5.5 5.5 0 0 0 2 8.5c0 2.3 1.5 4.05 3 5.5l7 7Z\"/>" },
        { "share",       "<path d=\"M4 12v8a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-8\"/>"
                         "<polyline points=\"16 6 12 2 8 6\"/>"
                         "<line x1=\"12\" y1=\"2\" x2=\"12\" y2=\"15\"/>" },
        { "lyrics",      "<path d=\"M9 18V5l12-2v13\"/>"
                         "<circle cx=\"6\" cy=\"18\" r=\"3\"/>"
                         "<circle cx=\"18\" cy=\"16\" r=\"3\"/>" },
        { "check",       "<polyline points=\"20 6 9 17 4 12\"/>" },
    };
    return kMap;
}

} // namespace mf::app::widgets::icons
