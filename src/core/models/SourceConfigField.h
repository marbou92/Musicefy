// SourceConfigField.h
// Describes a single configuration field a provider needs (for UI rendering).
// Port of Musicefy.Core.Models.SourceConfigField.

#pragma once

#include <QHash>
#include <QString>

namespace mf::core::models {

class SourceConfigField {
public:
    QString key() const { return key_; }
    void setKey(QString v) { key_ = std::move(v); }

    QString label() const { return label_; }
    void setLabel(QString v) { label_ = std::move(v); }

    QString description() const { return description_; }
    void setDescription(QString v) { description_ = std::move(v); }

    bool isPassword() const { return isPassword_; }
    void setIsPassword(bool v) { isPassword_ = v; }

    bool isRequired() const { return isRequired_; }
    void setIsRequired(bool v) { isRequired_ = v; }

    QString placeholder() const { return placeholder_; }
    void setPlaceholder(QString v) { placeholder_ = std::move(v); }

    QString defaultValue() const { return defaultValue_; }
    void setDefaultValue(QString v) { defaultValue_ = std::move(v); }

    /// "text" (default), "password", "select", "checkbox"
    QString fieldType() const { return fieldType_; }
    void setFieldType(QString v) { fieldType_ = std::move(v); }

    /// For "select" fields: key -> display label.
    const QHash<QString, QString>& options() const { return options_; }
    void setOptions(QHash<QString, QString> v) { options_ = std::move(v); }

private:
    QString key_;
    QString label_;
    QString description_;
    bool isPassword_ = false;
    bool isRequired_ = false;
    QString placeholder_;
    QString defaultValue_;
    QString fieldType_ = QStringLiteral("text");
    QHash<QString, QString> options_;
};

} // namespace mf::core::models
