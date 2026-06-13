// AddSourceDialog.cpp
// See header.

#include "AddSourceDialog.h"

#include "../core/models/SourceConfigField.h"
#include "../core/models/StreamingSource.h"
#include "../core/sources/StreamingSourceManager.h"
#include "../core/theme/MusicefyColorScheme.h"
#include "../core/theme/ThemeManager.h"

#include <QDialogButtonBox>
#include <QFormLayout>
#include <QGroupBox>
#include <QLabel>
#include <QLineEdit>
#include <QListWidget>
#include <QPushButton>
#include <QStackedWidget>
#include <QUuid>
#include <QVBoxLayout>

namespace mf::app::widgets {

using mf::core::models::SourceConfigField;
using mf::core::models::StreamingSource;
using mf::core::sources::StreamingSourceManager;
using mf::core::theme::ThemeManager;
using mf::core::theme::MusicefyColorScheme;

AddSourceDialog::AddSourceDialog(StreamingSourceManager* sourceMgr,
                                 ThemeManager*           theme,
                                 QWidget*                parent)
    : QDialog(parent)
    , sourceMgr_(sourceMgr)
    , theme_(theme)
{
    setWindowTitle(QStringLiteral("Add Source"));
    setMinimumSize(480, 420);
    buildUi();
    populateTypeList();
    applyTheme();

    if (theme_) {
        connect(theme_, &ThemeManager::schemeChanged,
                this, [this]() { applyTheme(); });
    }
}

void AddSourceDialog::buildUi() {
    auto* root = new QVBoxLayout(this);
    root->setContentsMargins(0, 0, 0, 0);
    root->setSpacing(0);

    stack_ = new QStackedWidget(this);

    // Page 1: type picker.
    auto* page1 = new QWidget(this);
    auto* p1root = new QVBoxLayout(page1);
    p1root->setContentsMargins(24, 20, 24, 16);
    p1root->setSpacing(12);

    auto* step1Label = new QLabel(QStringLiteral("Step 1: Choose source type"), page1);
    QFont slf = step1Label->font();
    slf.setBold(true);
    step1Label->setFont(slf);
    p1root->addWidget(step1Label);

    typeList_ = new QListWidget(page1);
    typeList_->setMinimumHeight(180);
    connect(typeList_, &QListWidget::itemDoubleClicked,
            this, &AddSourceDialog::onTypeSelected);
    p1root->addWidget(typeList_, /*stretch=*/1);

    auto* p1btnRow = new QHBoxLayout();
    p1btnRow->addStretch(1);
    nextBtn_ = new QPushButton(QStringLiteral("Next →"), page1);
    nextBtn_->setCursor(Qt::PointingHandCursor);
    nextBtn_->setEnabled(false);
    connect(nextBtn_, &QPushButton::clicked,
            this, &AddSourceDialog::onNextClicked);
    p1btnRow->addWidget(nextBtn_);
    p1root->addLayout(p1btnRow);

    stack_->addWidget(page1);

    // Page 2: dynamic form.
    formPage_ = new QWidget(this);
    auto* p2root = new QVBoxLayout(formPage_);
    p2root->setContentsMargins(24, 20, 24, 16);
    p2root->setSpacing(12);

    auto* step2Label = new QLabel(QStringLiteral("Step 2: Configure"), formPage_);
    QFont s2f = step2Label->font();
    s2f.setBold(true);
    step2Label->setFont(s2f);
    p2root->addWidget(step2Label);

    formLayout_ = new QFormLayout();
    formLayout_->setSpacing(10);
    p2root->addLayout(formLayout_, /*stretch=*/1);

    auto* p2btnRow = new QHBoxLayout();
    backBtn_ = new QPushButton(QStringLiteral("← Back"), formPage_);
    backBtn_->setCursor(Qt::PointingHandCursor);
    connect(backBtn_, &QPushButton::clicked,
            this, &AddSourceDialog::onBackClicked);
    p2btnRow->addWidget(backBtn_);
    p2btnRow->addStretch(1);
    addBtn_ = new QPushButton(QStringLiteral("Add ✓"), formPage_);
    addBtn_->setCursor(Qt::PointingHandCursor);
    connect(addBtn_, &QPushButton::clicked,
            this, &AddSourceDialog::onAddClicked);
    p2btnRow->addWidget(addBtn_);
    p2root->addLayout(p2btnRow);

    stack_->addWidget(formPage_);
    root->addWidget(stack_);
}

void AddSourceDialog::populateTypeList() {
    if (!typeList_ || !sourceMgr_) return;
    typeList_->clear();
    const auto types = sourceMgr_->registeredSourceTypes();
    for (const QString& type : types) {
        // Find the provider for the display name.
        auto prov = sourceMgr_->providerFor(type);
        const QString display = prov ? prov->displayName() : type;
        auto* item = new QListWidgetItem(display, typeList_);
        item->setData(Qt::UserRole, type);
    }
}

void AddSourceDialog::onTypeSelected(QListWidgetItem* item) {
    if (!item) return;
    typeList_->setCurrentItem(item);
    onNextClicked();
}

void AddSourceDialog::onNextClicked() {
    if (!typeList_ || !stack_) return;
    auto* item = typeList_->currentItem();
    if (!item) return;
    selectedType_ = item->data(Qt::UserRole).toString();
    buildFormForProvider(selectedType_);
    stack_->setCurrentIndex(1);
}

void AddSourceDialog::onBackClicked() {
    if (stack_) stack_->setCurrentIndex(0);
}

void AddSourceDialog::onAddClicked() {
    if (!sourceMgr_ || selectedType_.isEmpty()) return;

    StreamingSource src;
    src.setId(QUuid::createUuid().toString(QUuid::WithoutBraces));
    src.setType(selectedType_);
    src.setIsConnected(false);
    src.setClientVersion(QStringLiteral("1.0"));

    // Read field values from the form.
    for (const SourceConfigField& f : currentFields_) {
        auto it = fieldInputs_.constFind(f.key());
        if (it == fieldInputs_.constEnd()) continue;
        const QString val = it.value()->text().trimmed();

        // Map known keys to StreamingSource fields.
        const QString key = f.key();
        if (key == QStringLiteral("name") || key == QStringLiteral("displayName")) {
            src.setName(val);
        } else if (key == QStringLiteral("url") || key == QStringLiteral("serverUrl")) {
            src.setUrl(val);
        } else if (key == QStringLiteral("username")) {
            src.setUsername(val);
        } else if (key == QStringLiteral("password")) {
            src.setPassword(val);
        }
    }

    // Fallback: if name wasn't in config fields, use first non-empty.
    if (src.name().isEmpty()) {
        for (const SourceConfigField& f : currentFields_) {
            auto it = fieldInputs_.constFind(f.key());
            if (it != fieldInputs_.constEnd() && !it.value()->text().trimmed().isEmpty()) {
                src.setName(it.value()->text().trimmed());
                break;
            }
        }
    }

    result_ = src;
    accept();
}

void AddSourceDialog::buildFormForProvider(const QString& sourceType) {
    clearForm();
    if (!sourceMgr_) return;
    auto prov = sourceMgr_->providerFor(sourceType);
    if (!prov) return;

    currentFields_ = prov->configFields();

    // Update step 2 label.
    if (auto* label = formPage_->findChild<QLabel*>()) {
        label->setText(QStringLiteral("Step 2: Configure %1").arg(prov->displayName()));
    }

    // Always add a "Display name" field at the top.
    {
        auto* nameLe = new QLineEdit(formPage_);
        nameLe->setPlaceholderText(QStringLiteral("My %1").arg(prov->displayName()));
        fieldInputs_[QStringLiteral("name")] = nameLe;
        formLayout_->addRow(QStringLiteral("Display name:"), nameLe);
    }

    for (const SourceConfigField& f : currentFields_) {
        auto* le = new QLineEdit(formPage_);
        le->setPlaceholderText(f.placeholder());
        if (!f.defaultValue().isEmpty()) {
            le->setText(f.defaultValue());
        }
        if (f.isPassword()) {
            le->setEchoMode(QLineEdit::Password);
        }
        fieldInputs_[f.key()] = le;
        formLayout_->addRow(f.label() + QStringLiteral(":"), le);
    }
}

void AddSourceDialog::clearForm() {
    currentFields_.clear();
    fieldInputs_.clear();
    if (!formLayout_) return;
    while (formLayout_->count() > 0) {
        auto* item = formLayout_->takeAt(0);
        if (item->widget()) item->widget()->deleteLater();
        delete item;
    }
}

void AddSourceDialog::applyTheme() {
    MusicefyColorScheme s;
    if (theme_) s = theme_->scheme();
    if (!s.primary.isValid()) {
        setStyleSheet(QString());
        return;
    }

    setStyleSheet(QStringLiteral(
        "QDialog { background: %1; color: %2; }"
        "QLabel { color: %2; background: transparent; }"
        "QListWidget {"
        "  background: %3; color: %2;"
        "  border: 1px solid %4; border-radius: 6px;"
        "  padding: 4px;"
        "}"
        "QListWidget::item {"
        "  padding: 8px 12px; border-radius: 4px;"
        "}"
        "QListWidget::item:selected { background: %5; color: %6; }"
        "QListWidget::item:hover { background: %7; }"
        "QLineEdit {"
        "  background: %3; color: %2;"
        "  border: 1px solid %4; border-radius: 6px; padding: 6px 10px;"
        "  selection-background-color: %5;"
        "}"
        "QLineEdit:focus { border: 1px solid %5; }"
        "QPushButton {"
        "  background: %5; color: %6;"
        "  border: none; border-radius: 6px; padding: 8px 18px;"
        "  font-weight: bold;"
        "}"
        "QPushButton:hover { background: %8; }"
        "QPushButton:disabled { background: %7; color: %9; }"
    )
    .arg(s.background.name())           // 1
    .arg(s.onSurface.name())            // 2
    .arg(s.surfaceContainerHigh.name()) // 3
    .arg(s.outlineVariant.name())       // 4
    .arg(s.primaryContainer.name())     // 5
    .arg(s.onPrimaryContainer.name())   // 6
    .arg(s.surfaceContainer.name())     // 7
    .arg(s.surfaceContainerHighest.name()) // 8
    .arg(s.onSurfaceVariant.name())     // 9
    );
}

} // namespace mf::app::widgets
