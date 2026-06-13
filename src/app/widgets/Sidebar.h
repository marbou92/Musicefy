// Sidebar.h
// Left-hand navigation column. Four tabs: Home, Search, Library,
// Settings. Visually it's a vertical list of buttons; functionally
// it forwards the selection to a MainViewModel.
//
// Styling is done via QSS in applyTheme() — the widget pulls the
// current scheme from a ThemeManager and rebuilds its stylesheet
// on every schemeChanged signal.

#pragma once

#include <QWidget>

class QListWidget;
class QListWidgetItem;

namespace mf::app::viewmodels { class MainViewModel; }
namespace mf::core::theme     { class ThemeManager; }

namespace mf::app::widgets {

class Sidebar : public QWidget {
    Q_OBJECT
public:
    explicit Sidebar(mf::app::viewmodels::MainViewModel* vm,
                     mf::core::theme::ThemeManager*        theme,
                     QWidget* parent = nullptr);
    ~Sidebar() override = default;

    QSize sizeHint() const override { return QSize(208, 0); }
    QSize minimumSizeHint() const override { return QSize(180, 0); }

private slots:
    void onRowChanged(int currentRow);
    void onVmPageChanged();
    void onThemeChanged();

private:
    void buildUi();
    void applyTheme();
    void populate();

    mf::app::viewmodels::MainViewModel* vm_    = nullptr;
    mf::core::theme::ThemeManager*        theme_ = nullptr;

    QListWidget* list_ = nullptr;
};

} // namespace mf::app::widgets
