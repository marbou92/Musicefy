// PlaylistPickerDialog.h
// Modal dialog that shows a list of local playlists and lets the user
// pick one (or create a new one) to add tracks to.

#pragma once

#include <QDialog>

class QListView;
class QStandardItemModel;

namespace mf::app::viewmodels { class LibraryViewModel; }
namespace mf::core::theme    { class ThemeManager; }

namespace mf::app::widgets {

class PlaylistPickerDialog : public QDialog {
    Q_OBJECT
public:
    PlaylistPickerDialog(mf::app::viewmodels::LibraryViewModel* libVm,
                         mf::core::theme::ThemeManager*        theme,
                         QWidget* parent = nullptr);
    ~PlaylistPickerDialog() override = default;

    /// Returns the selected playlist ID (empty if cancelled).
    QString selectedPlaylistId() const { return selectedId_; }
    /// Returns the selected playlist name.
    QString selectedPlaylistName() const { return selectedName_; }

private slots:
    void onSelectionChanged();
    void onNewPlaylistClicked();
    void onAccepted();

private:
    void buildUi();
    void applyTheme();
    void refreshList();

    mf::app::viewmodels::LibraryViewModel* libVm_ = nullptr;
    mf::core::theme::ThemeManager*         theme_ = nullptr;

    QListView*          listView_    = nullptr;
    QStandardItemModel* listModel_   = nullptr;
    QPushButton*        newPlaylistBtn_ = nullptr;
    QString             selectedId_;
    QString             selectedName_;
};

} // namespace mf::app::widgets
