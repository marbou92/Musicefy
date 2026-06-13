#pragma once

#include "SourceConfigField.h"

#include <QList>
#include <QString>

namespace mf::core::models {

class ExtensionManifest {
public:
    QString id() const { return id_; }
    void setId(QString v) { id_ = std::move(v); }

    QString name() const { return name_; }
    void setName(QString v) { name_ = std::move(v); }

    QString version() const { return version_; }
    void setVersion(QString v) { version_ = std::move(v); }

    QString author() const { return author_; }
    void setAuthor(QString v) { author_ = std::move(v); }

    QString description() const { return description_; }
    void setDescription(QString v) { description_ = std::move(v); }

    QString sourceType() const { return sourceType_; }
    void setSourceType(QString v) { sourceType_ = std::move(v); }

    QString entryPoint() const { return entryPoint_; }
    void setEntryPoint(QString v) { entryPoint_ = std::move(v); }

    QString filePath() const { return filePath_; }
    void setFilePath(QString v) { filePath_ = std::move(v); }

    bool isEnabled() const { return isEnabled_; }
    void setIsEnabled(bool v) { isEnabled_ = v; }

    QList<SourceConfigField> configFields() const { return configFields_; }
    void setConfigFields(QList<SourceConfigField> v) { configFields_ = std::move(v); }

private:
    QString id_;
    QString name_;
    QString version_;
    QString author_;
    QString description_;
    QString sourceType_;
    QString entryPoint_;
    QString filePath_;
    bool isEnabled_ = false;
    QList<SourceConfigField> configFields_;
};

} // namespace mf::core::models
