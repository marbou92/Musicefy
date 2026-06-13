// CreatePlaylistDialog.cpp

#include "CreatePlaylistDialog.h"

#include "../viewmodels/LibraryViewModel.h"
#include "../../core/models/PlaylistInfo.h"
#include "../../core/theme/ThemeManager.h"
#include "../../core/theme/MusicefyColorScheme.h"

#include <QVBoxLayout>
#include <QHBoxLayout>
#include <QLineEdit>
#include <QTextEdit>
#include <QLabel>
#include <QPushButton>
#include <QFileDialog>
#include <QPixmap>
#include <QUuid>

namespace mf::app::widgets {

using mf::app::viewmodels::LibraryViewModel;
using mf::core::theme::ThemeManager;
using mf::core::theme::MusicefyColorScheme;
using mf::core::models::PlaylistInfo;

CreatePlaylistDialog::CreatePlaylistDialog(LibraryViewModel* libVm,
                                           ThemeManager*     theme,
                                           QWidget*          parent)
    : QDialog(parent)
    , libVm_(libVm)
    , theme_(theme)
{
    setWindowTitle(QStringLiteral("Create Playlist"));
    setMinimumSize(480, 460);
    buildUi();
    applyTheme();

    if (theme_) {
        connect(theme_, &ThemeManager::schemeChanged,
                this, [this]() { applyTheme(); });
    }
}

// ──────────────────────────────────────────────────────────────────
void CreatePlaylistDialog::buildUi()
{
    auto* root = new QVBoxLayout(this);
    root->setContentsMargins(24, 20, 24, 20);
    root->setSpacing(14);

    // ── Title ──────────────────────────────────────────────────────
    auto* title = new QLabel(QStringLiteral("Create Playlist"));
    QFont tf = title->font();
    tf.setPointSize(14);
    tf.setBold(true);
    title->setFont(tf);
    root->addWidget(title);

    // ── Name input (required) ─────────────────────────────────────
    auto* nameLabel = new QLabel(QStringLiteral("Name *"));
    QFont nlb = nameLabel->font();
    nlb.setBold(true);
    nameLabel->setFont(nlb);
    root->addWidget(nameLabel);

    nameEdit_ = new QLineEdit;
    nameEdit_->setPlaceholderText(QStringLiteral("Playlist name"));
    connect(nameEdit_, &QLineEdit::textChanged,
            this, &CreatePlaylistDialog::onNameChanged);
    root->addWidget(nameEdit_);

    // ── Description input (optional) ──────────────────────────────
    auto* descLabel = new QLabel(QStringLiteral("Description"));
    root->addWidget(descLabel);

    descEdit_ = new QTextEdit;
    descEdit_->setPlaceholderText(QStringLiteral("Description (optional)"));
    descEdit_->setMinimumHeight(80);
    descEdit_->setMaximumHeight(120);
    root->addWidget(descEdit_);

    // ── Cover image row ───────────────────────────────────────────
    auto* coverLabel = new QLabel(QStringLiteral("Cover Image"));
    root->addWidget(coverLabel);

    auto* coverRow = new QHBoxLayout;
    coverRow->setSpacing(14);

    coverPreview_ = new QLabel;
    coverPreview_->setFixedSize(120, 120);
    coverPreview_->setAlignment(Qt::AlignCenter);
    coverPreview_->setText(QStringLiteral("No cover"));
    QFont cpf = coverPreview_->font();
    cpf.setPointSize(9);
    coverPreview_->setFont(cpf);
    coverRow->addWidget(coverPreview_);

    auto* coverBtnCol = new QVBoxLayout;
    coverBtnCol->setSpacing(8);

    coverBtn_ = new QPushButton(QStringLiteral("Browse..."));
    coverBtn_->setCursor(Qt::PointingHandCursor);
    connect(coverBtn_, &QPushButton::clicked,
            this, &CreatePlaylistDialog::onBrowseCover);
    coverBtnCol->addWidget(coverBtn_);

    coverBtnCol->addStretch(1);
    coverRow->addLayout(coverBtnCol, 1);

    root->addLayout(coverRow);

    // ── Bottom row: Cancel / Create ───────────────────────────────
    root->addStretch(1);

    auto* bottomRow = new QHBoxLayout;

    auto* cancelBtn = new QPushButton(QStringLiteral("Cancel"));
    cancelBtn->setCursor(Qt::PointingHandCursor);
    connect(cancelBtn, &QPushButton::clicked, this, &QDialog::reject);
    bottomRow->addWidget(cancelBtn);

    bottomRow->addStretch(1);

    createBtn_ = new QPushButton(QStringLiteral("Create"));
    createBtn_->setCursor(Qt::PointingHandCursor);
    createBtn_->setEnabled(false);
    connect(createBtn_, &QPushButton::clicked,
            this, &CreatePlaylistDialog::onAccepted);
    bottomRow->addWidget(createBtn_);

    root->addLayout(bottomRow);
}

// ──────────────────────────────────────────────────────────────────
void CreatePlaylistDialog::applyTheme()
{
    MusicefyColorScheme s;
    if (theme_) s = theme_->scheme();
    if (!s.surface.isValid()) return;

    const bool isRounded = !coverPath_.isEmpty();
    Q_UNUSED(isRounded);

    setStyleSheet(QStringLiteral(
        "QDialog { background: %1; color: %2; }"
        "QLabel { color: %2; background: transparent; }"
        "QLineEdit {"
        "  background: %3; color: %2;"
        "  border: 1px solid %4; border-radius: 6px; padding: 8px 10px;"
        "  selection-background-color: %5;"
        "}"
        "QLineEdit:focus { border: 1px solid %5; }"
        "QTextEdit {"
        "  background: %3; color: %2;"
        "  border: 1px solid %4; border-radius: 6px; padding: 6px 8px;"
        "  selection-background-color: %5;"
        "}"
        "QTextEdit:focus { border: 1px solid %5; }"
        "QPushButton {"
        "  background: %3; color: %2; border: 1px solid %4;"
        "  border-radius: 8px; padding: 8px 16px; font-size: 13px;"
        "}"
        "QPushButton:hover { background: %7; }"
        "QPushButton:disabled { color: %8; }"
        "QPushButton[objectName=\"createBtn\"] {"
        "  background: %5; color: %6; border: 1px solid %5;"
        "}"
        "QPushButton[objectName=\"createBtn\"]:hover {"
        "  background: %9; border: 1px solid %9;"
        "}"
        "QLabel[objectName=\"coverPreview\"] {"
        "  background: %3; color: %8; border: 1px solid %4;"
        "  border-radius: 12px;"
        "}"
    )
    .arg(s.surface.name(),                // 1
         s.onSurface.name(),              // 2
         s.surfaceContainerHigh.name(),   // 3
         s.outlineVariant.name(),         // 4
         s.primary.name(),                // 5
         s.onPrimary.name(),              // 6
         s.surfaceContainerHighest.name(),// 7
         s.onSurfaceVariant.name(),       // 8
         s.primaryContainer.name()));     // 9

    // Object-name selectors must also be set on the widgets.
    if (createBtn_) createBtn_->setObjectName(QStringLiteral("createBtn"));
    if (coverPreview_) coverPreview_->setObjectName(QStringLiteral("coverPreview"));
}

// ──────────────────────────────────────────────────────────────────
void CreatePlaylistDialog::onNameChanged(const QString& text)
{
    if (createBtn_)
        createBtn_->setEnabled(!text.trimmed().isEmpty());
}

// ──────────────────────────────────────────────────────────────────
void CreatePlaylistDialog::onBrowseCover()
{
    const QString filePath = QFileDialog::getOpenFileName(
        this,
        QStringLiteral("Choose Cover Image"),
        QString(),
        QStringLiteral("Images (*.png *.jpg *.jpeg *.bmp)"));

    if (filePath.isEmpty()) return;

    coverPath_ = filePath;

    // Show a thumbnail preview in the cover label.
    QPixmap pix(filePath);
    if (!pix.isNull()) {
        coverPreview_->setPixmap(
            pix.scaled(120, 120, Qt::KeepAspectRatio, Qt::SmoothTransformation));
    }
}

// ──────────────────────────────────────────────────────────────────
void CreatePlaylistDialog::onAccepted()
{
    const QString name = nameEdit_->text().trimmed();
    if (name.isEmpty()) return;

    const QString desc = descEdit_->toPlainText().trimmed();

    // Build the PlaylistInfo we will return.
    PlaylistInfo info;
    info.setId(QUuid::createUuid().toString(QUuid::WithoutBraces));
    info.setName(name);
    info.setDescription(desc);
    info.setCoverPath(coverPath_);
    info.setCreatedAt(QDateTime::currentDateTime());
    info.setTrackCount(0);

    // Delegate to the view-model which persists to the repository.
    if (libVm_)
        libVm_->createPlaylist(name);

    createdPlaylist_ = info;
    accept();
}

} // namespace mf::app::widgets
