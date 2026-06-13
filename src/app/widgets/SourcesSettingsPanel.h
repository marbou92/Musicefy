// SourcesSettingsPanel.h
// Settings sub-panel for streaming source accounts. Replaces the
// Sources stub.
//
//   ┌──────────────────────────────────────────────────────────┐
//   │ Sources                                                │
//   │ Configure streaming accounts (Subsonic, YouTube, etc).  │
//   │                                                         │
//   │ Configured sources                                      │
//   │  ┌──────────────────────────────────────────────────┐  │
//   │  │ Name          Type      URL            Status    │  │
//   │  │ My Navidrome  Subsonic  https://…       Connected │  │
//   │  │ YT Browse     YouTube   (api-key)       —         │  │
//   │  └──────────────────────────────────────────────────┘  │
//   │  [Add Subsonic…]  [Remove selected]                     │
//   │                                                         │
//   │ Notes                                                   │
//   │  YouTube sources don't need a URL — they use an API     │
//   │  key only. Per-source credentials are stored locally    │
//   │  in your QSettings file (passwords in plain text).      │
//   └──────────────────────────────────────────────────────────┘
//
// For the first cut, only "Add Subsonic…" is implemented.
// YouTube / Local / extension sources can be added similarly.

#pragma once

#include <QWidget>

class QLabel;
class QListView;
class QPushButton;
class QStandardItemModel;

namespace mf::core::sources { class StreamingSourceManager; }
namespace mf::core::theme   { class ThemeManager; }

namespace mf::app::widgets {

class SourcesSettingsPanel : public QWidget {
    Q_OBJECT
public:
    SourcesSettingsPanel(mf::core::sources::StreamingSourceManager* sourceMgr,
                         mf::core::theme::ThemeManager*            theme,
                         QWidget* parent = nullptr);
    ~SourcesSettingsPanel() override = default;

private slots:
    void onAddSubsonicClicked();
    void onAddOtherClicked();
    void onRemoveClicked();
    void onSourceAdded();
    void onSourceUpdated();
    void onSourceRemoved();

private:
    void buildUi();
    void applyTheme();
    void refreshList();

    mf::core::sources::StreamingSourceManager* sourceMgr_ = nullptr;
    mf::core::theme::ThemeManager*             theme_    = nullptr;

    QListView*          list_    = nullptr;
    QStandardItemModel* model_   = nullptr;
    QPushButton*        addBtn_  = nullptr;
    QPushButton*        addOtherBtn_ = nullptr;
    QPushButton*        removeBtn_ = nullptr;
};

} // namespace mf::app::widgets
