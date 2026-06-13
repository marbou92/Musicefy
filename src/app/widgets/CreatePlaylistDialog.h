// CreatePlaylistDialog.h
// Modal dialog for creating a new playlist with name, optional
// description, and optional cover image.

#pragma once

#include <QDialog>
#include "../../core/models/PlaylistInfo.h"

class QLineEdit;
class QTextEdit;
class QLabel;
class QPushButton;

namespace mf::app::viewmodels { class LibraryViewModel; }
namespace mf::core::theme    { class ThemeManager; }

namespace mf::app::widgets {

class CreatePlaylistDialog : public QDialog {
    Q_OBJECT
public:
    CreatePlaylistDialog(mf::app::viewmodels::LibraryViewModel* libVm,
                         mf::core::theme::ThemeManager*        theme,
                         QWidget* parent = nullptr);
    ~CreatePlaylistDialog() override = default;

    /// Returns the playlist info populated on accept (id, name,
    /// description, coverPath, createdAt). Default-constructed
    /// if the user cancelled.
    mf::core::models::PlaylistInfo createdPlaylist() const { return createdPlaylist_; }

private slots:
    void onBrowseCover();
    void onAccepted();
    void onNameChanged(const QString& text);

private:
    void buildUi();
    void applyTheme();

    mf::app::viewmodels::LibraryViewModel* libVm_ = nullptr;
    mf::core::theme::ThemeManager*         theme_ = nullptr;

    QLineEdit*  nameEdit_     = nullptr;
    QTextEdit*  descEdit_     = nullptr;
    QPushButton* coverBtn_    = nullptr;
    QLabel*     coverPreview_ = nullptr;
    QPushButton* createBtn_   = nullptr;
    QString     coverPath_;
    mf::core::models::PlaylistInfo createdPlaylist_;
};

} // namespace mf::app::widgets
