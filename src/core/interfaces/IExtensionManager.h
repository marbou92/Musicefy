#pragma once

#include "../models/ExtensionManifest.h"

#include <QString>

#include <functional>
#include <memory>

namespace mf::core::interfaces {

class IExtensionManager {
public:
    virtual ~IExtensionManager() = default;

    using ManifestCallback = std::function<void(QList<mf::core::models::ExtensionManifest>)>;
    using LoadCallback     = std::function<void(bool ok, QString errorMessage)>;

    virtual void loadExtensions(QString directory, ManifestCallback onDone) = 0;
    virtual QList<mf::core::models::ExtensionManifest> allExtensions() const = 0;
    virtual QList<mf::core::models::ExtensionManifest> enabledExtensions() const = 0;
    virtual void enableExtension(QString id) = 0;
    virtual void disableExtension(QString id) = 0;
    virtual bool isLoaded(QString id) const = 0;
    virtual void* resolveEntrypoint(QString id) = 0; // returns void*; cast in service
};

} // namespace mf::core::interfaces
