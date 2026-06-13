// AddSourceDialog.h
// Two-step wizard for adding a streaming source. Page 1: pick the
// source type from a list of registered providers. Page 2: fill in
// the configuration fields returned by the provider's configFields().
//
//   ┌──────────────────────────────────────────────────┐
//   │ Add Source                                [Close]│
//   │                                                  │
//   │  Step 1: Choose source type                      │
//   │  ┌────────────────────────────────────────────┐  │
//   │  │ ● Subsonic (Navidrome, Airsonic, …)       │  │
//   │  │ ○ YouTube Music                            │  │
//   │  └────────────────────────────────────────────┘  │
//   │                                  [Next →]        │
//   ├──────────────────────────────────────────────────┤
//   │ Add Source                                [Close]│
//   │                                                  │
//   │  Step 2: Configure Subsonic                      │
//   │  Display name: [________________]                │
//   │  Server URL:   [________________]                │
//   │  Username:     [________________]                │
//   │  Password:     [________________]                │
//   │                                  [Add ✓]         │
//   └──────────────────────────────────────────────────┘

#pragma once

#include <QDialog>
#include <QList>
#include <QHash>

class QStackedWidget;
class QListWidget;
class QListWidgetItem;
class QLineEdit;
class QFormLayout;

#include "../../core/models/StreamingSource.h"
#include "../../core/models/SourceConfigField.h"

namespace mf::core::sources  { class StreamingSourceManager; }
namespace mf::core::theme    { class ThemeManager; }

namespace mf::app::widgets {

class AddSourceDialog : public QDialog {
    Q_OBJECT
public:
    AddSourceDialog(mf::core::sources::StreamingSourceManager* sourceMgr,
                    mf::core::theme::ThemeManager*            theme,
                    QWidget* parent = nullptr);
    ~AddSourceDialog() override = default;

    /// Returns the configured source if the user clicked Add,
    /// otherwise returns a default-constructed (invalid) source.
    mf::core::models::StreamingSource resultSource() const { return result_; }

private slots:
    void onTypeSelected(QListWidgetItem* item);
    void onNextClicked();
    void onAddClicked();
    void onBackClicked();

private:
    void buildUi();
    void buildPage1();
    void buildPage2();
    void applyTheme();
    void populateTypeList();
    void buildFormForProvider(const QString& sourceType);
    void clearForm();

    mf::core::sources::StreamingSourceManager* sourceMgr_ = nullptr;
    mf::core::theme::ThemeManager*             theme_    = nullptr;

    QStackedWidget* stack_     = nullptr;
    QListWidget*    typeList_  = nullptr;
    QPushButton*    nextBtn_   = nullptr;
    QPushButton*    backBtn_   = nullptr;
    QPushButton*    addBtn_    = nullptr;
    QWidget*        formPage_  = nullptr;
    QFormLayout*    formLayout_ = nullptr;

    // Current selection state.
    QString selectedType_;
    QList<mf::core::models::SourceConfigField> currentFields_;
    QHash<QString, QLineEdit*> fieldInputs_;
    mf::core::models::StreamingSource result_;
};

} // namespace mf::app::widgets
