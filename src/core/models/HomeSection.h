#pragma once

#include "BrowseSection.h"

#include <QList>

namespace mf::core::models {

class HomeSection {
public:
    HomeSection() = default;

    QString id() const { return id_; }
    void setId(QString v) { id_ = std::move(v); }

    QString title() const { return title_; }
    void setTitle(QString v) { title_ = std::move(v); }

    QList<BrowseSection> children() const { return children_; }
    void setChildren(QList<BrowseSection> v) { children_ = std::move(v); }

    int order() const { return order_; }
    void setOrder(int v) { order_ = v; }

    bool isEnabled() const { return isEnabled_; }
    void setIsEnabled(bool v) { isEnabled_ = v; }

private:
    QString id_;
    QString title_;
    QList<BrowseSection> children_;
    int order_ = 0;
    bool isEnabled_ = true;
};

} // namespace mf::core::models
