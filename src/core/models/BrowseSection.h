#pragma once

#include "MusicFile.h"

#include <QList>
#include <QString>

namespace mf::core::models {

class BrowseSection {
public:
    BrowseSection() = default;

    QString id() const { return id_; }
    void setId(QString v) { id_ = std::move(v); }

    QString title() const { return title_; }
    void setTitle(QString v) { title_ = std::move(v); }

    /// "Grid", "List", "HorizontalCarousel"
    QString layout() const { return layout_; }
    void setLayout(QString v) { layout_ = std::move(v); }

    QString sourceType() const { return sourceType_; }
    void setSourceType(QString v) { sourceType_ = std::move(v); }

    QList<MusicFile> items() const { return items_; }
    void setItems(QList<MusicFile> v) { items_ = std::move(v); }

private:
    QString id_;
    QString title_;
    QString layout_ = QStringLiteral("HorizontalCarousel");
    QString sourceType_;
    QList<MusicFile> items_;

    friend bool operator==(const BrowseSection& a, const BrowseSection& b) {
        return a.id_ == b.id_
            && a.title_ == b.title_
            && a.layout_ == b.layout_
            && a.sourceType_ == b.sourceType_
            && a.items_ == b.items_;
    }
    friend bool operator!=(const BrowseSection& a, const BrowseSection& b) {
        return !(a == b);
    }
};

} // namespace mf::core::models
