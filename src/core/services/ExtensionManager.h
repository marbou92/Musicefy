// ExtensionManager.h
// Loads third-party music source extensions from a directory. Each
// extension is a shared library (DLL on Windows, DSO on Linux) that
// exports a single C function:
//
//   mf_extension_abi_t* mf_extension_init(void);
//
// The returned struct is a flat C vtable (see ExtensionManifest for the
// C-side shape; we'll keep the loader generic via a function-pointer
// table so the loader doesn't need to know about provider specifics).
//
// The Manager:
//   - Walks <extensions> for *.dll / *.so / *.dylib
//   - Loads each, calls mf_extension_init, reads its manifest
//   - Tracks loaded extensions in memory
//   - Exposes enable/disable (which is a soft toggle — loaded libraries
//     stay mapped but their providers are skipped during routing)
//
// Security: the loader does not sandbox extensions. Extensions are
// trusted first-party code. Don't load DLLs from untrusted locations.

#pragma once

#include "../interfaces/IExtensionManager.h"

#include <QObject>
#include "../models/ExtensionManifest.h"

#include <QHash>
#include <QList>
#include <QString>

#include <memory>

namespace mf::core::services {

class ExtensionManager : public QObject,
                         public mf::core::interfaces::IExtensionManager {
    Q_OBJECT
public:
    explicit ExtensionManager(QObject* parent = nullptr);
    ~ExtensionManager() override;

    void loadExtensions(QString directory, ManifestCallback onDone) override;
    QList<mf::core::models::ExtensionManifest> allExtensions() const override;
    QList<mf::core::models::ExtensionManifest> enabledExtensions() const override;
    void enableExtension(QString id) override;
    void disableExtension(QString id) override;
    bool isLoaded(QString id) const override;
    void* resolveEntrypoint(QString id) override;

signals:
    void extensionsLoadedQ();
    void extensionEnabledQ(QString id);
    void extensionDisabledQ(QString id);

private:
    struct Loaded {
        mf::core::models::ExtensionManifest manifest;
        void*                               handle      = nullptr; // OS handle
        void*                               entrypoint  = nullptr; // resolved fn ptr
    };

    QHash<QString, Loaded> loaded_;
};

} // namespace mf::core::services