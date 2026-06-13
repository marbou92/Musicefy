// ExtensionManager.cpp
// Loads extension DLLs from a directory. Each DLL exports the C function:
//
//   mf_extension_abi_t* mf_extension_init(void);
//
// where mf_extension_abi_t is a flat struct of function pointers. The
// loader is intentionally minimal: if a DLL can't be loaded, or the
// entry point can't be resolved, it's silently skipped (with a warning
// logged). The first byte of the ABI struct is interpreted as a
// manifest JSON; the rest is the function-pointer table.

#include "ExtensionManager.h"

#include <QDir>
#include <QFileInfo>
#include <QJsonDocument>
#include <QJsonObject>
#include <QLibrary>
#include <QJsonArray>

namespace mf::core::services {

using mf::core::interfaces::IExtensionManager;
using mf::core::models::ExtensionManifest;
using mf::core::models::SourceConfigField;

namespace {

QStringList libraryFiltersForPlatform() {
#if defined(Q_OS_WIN)
    return { QStringLiteral("*.dll") };
#elif defined(Q_OS_MAC)
    return { QStringLiteral("*.dylib") };
#else
    return { QStringLiteral("*.so") };
#endif
}

} // namespace

ExtensionManager::ExtensionManager(QObject* parent)
    : QObject(parent)
{
}

ExtensionManager::~ExtensionManager() = default;

void ExtensionManager::loadExtensions(QString directory, ManifestCallback onDone) {
    QDir d(directory);
    if (!d.exists()) {
        if (onDone) onDone(allExtensions());
        return;
    }

    QStringList filters = libraryFiltersForPlatform();
    QStringList entries = d.entryList(filters, QDir::Files, QDir::Name);

    for (const QString& name : entries) {
        QString fullPath = d.absoluteFilePath(name);
        QLibrary lib(fullPath);
        if (!lib.load()) {
            qWarning() << "ExtensionManager: failed to load" << fullPath
                       << ":" << lib.errorString();
            continue;
        }
        // The C ABI requires exactly this symbol name.
        using InitFn = void* (*)();
        InitFn init = reinterpret_cast<InitFn>(lib.resolve("mf_extension_init"));
        if (!init) {
            qWarning() << "ExtensionManager: no mf_extension_init in" << fullPath;
            lib.unload();
            continue;
        }
        void* abi = init();
        if (!abi) {
            qWarning() << "ExtensionManager: mf_extension_init returned null in" << fullPath;
            lib.unload();
            continue;
        }
        // First N bytes: manifest JSON (null-terminated). Read the prefix
        // as a C string; cast to const char* and parse.
        const char* manifestJson = *reinterpret_cast<const char**>(abi);
        if (!manifestJson) {
            qWarning() << "ExtensionManager: null manifest in" << fullPath;
            lib.unload();
            continue;
        }
        QJsonDocument doc = QJsonDocument::fromJson(QByteArray(manifestJson));
        if (!doc.isObject()) {
            qWarning() << "ExtensionManager: manifest is not a JSON object in" << fullPath;
            lib.unload();
            continue;
        }
        QJsonObject obj = doc.object();

        ExtensionManifest m;
        m.setId(obj.value(QStringLiteral("id")).toString());
        m.setName(obj.value(QStringLiteral("name")).toString());
        m.setVersion(obj.value(QStringLiteral("version")).toString());
        m.setAuthor(obj.value(QStringLiteral("author")).toString());
        m.setDescription(obj.value(QStringLiteral("description")).toString());
        m.setSourceType(obj.value(QStringLiteral("sourceType")).toString());
        m.setEntryPoint(obj.value(QStringLiteral("entryPoint")).toString(QStringLiteral("mf_extension_init")));
        m.setFilePath(fullPath);

        if (m.id().isEmpty() || m.sourceType().isEmpty()) {
            qWarning() << "ExtensionManager: missing id or sourceType in" << fullPath;
            lib.unload();
            continue;
        }

        // Parse config fields, if any.
        QList<SourceConfigField> fields;
        for (const QJsonValue& v : obj.value(QStringLiteral("configFields")).toArray()) {
            QJsonObject fo = v.toObject();
            SourceConfigField f;
            f.setKey(fo.value(QStringLiteral("key")).toString());
            f.setLabel(fo.value(QStringLiteral("label")).toString());
            f.setPlaceholder(fo.value(QStringLiteral("placeholder")).toString());
            f.setDefaultValue(fo.value(QStringLiteral("defaultValue")).toString());
            f.setFieldType(fo.value(QStringLiteral("fieldType")).toString(QStringLiteral("text")));
            f.setIsRequired(fo.value(QStringLiteral("required")).toBool());
            f.setIsPassword(fo.value(QStringLiteral("password")).toBool());
            fields.append(f);
        }
        m.setConfigFields(fields);

        Loaded l;
        l.manifest = m;
        l.entrypoint = abi;
        l.handle = QLibrary::isLibrary(fullPath) ? lib.isLoaded() ? &lib : nullptr
                                                  : nullptr;
        // We can't store the QLibrary as a void* portably; the loaded
        // extension will be unloaded when this manager is destroyed if
        // we tracked each QLibrary. For now we leak the QLibrary to
        // keep the entrypoint valid for the lifetime of the app. A
        // future iteration will track QLibraries in a vector.
        static QList<QLibrary*> libRegistry;
        libRegistry.append(new QLibrary(fullPath));

        loaded_.insert(m.id(), l);
    }

    emit extensionsLoadedQ();
    if (onDone) onDone(allExtensions());
}

QList<ExtensionManifest> ExtensionManager::allExtensions() const {
    QList<ExtensionManifest> out;
    for (auto it = loaded_.constBegin(); it != loaded_.constEnd(); ++it) {
        out.append(it->manifest);
    }
    return out;
}

QList<ExtensionManifest> ExtensionManager::enabledExtensions() const {
    QList<ExtensionManifest> out;
    for (auto it = loaded_.constBegin(); it != loaded_.constEnd(); ++it) {
        if (it->manifest.isEnabled()) {
            out.append(it->manifest);
        }
    }
    return out;
}

void ExtensionManager::enableExtension(QString id) {
    auto it = loaded_.find(id);
    if (it == loaded_.end()) return;
    it->manifest.setIsEnabled(true);
    emit extensionEnabledQ(id);
}

void ExtensionManager::disableExtension(QString id) {
    auto it = loaded_.find(id);
    if (it == loaded_.end()) return;
    it->manifest.setIsEnabled(false);
    emit extensionDisabledQ(id);
}

bool ExtensionManager::isLoaded(QString id) const {
    return loaded_.contains(id);
}

void* ExtensionManager::resolveEntrypoint(QString id) {
    auto it = loaded_.find(id);
    if (it == loaded_.end()) return nullptr;
    return it->entrypoint;
}

} // namespace mf::core::services