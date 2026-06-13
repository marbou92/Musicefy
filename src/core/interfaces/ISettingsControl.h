#pragma once

#include <QString>
#include <QVariant>

#include <functional>
#include <optional>

namespace mf::core::interfaces {

class ISettingsControl {
public:
    virtual ~ISettingsControl() = default;

    virtual QVariant get(QString key) const = 0;
    virtual void      set(QString key, QVariant value) = 0;
    virtual bool      contains(QString key) const = 0;
    virtual void      remove(QString key) = 0;
    virtual void      sync() = 0;

    template <typename T>
    std::optional<T> getAs(QString key) const {
        const QVariant v = get(key);
        if (!v.isValid() || !v.canConvert<T>()) {
            return std::nullopt;
        }
        return v.value<T>();
    }

    template <typename T>
    T getOrDefault(QString key, T defaultValue) const {
        const QVariant v = get(key);
        if (!v.isValid() || !v.canConvert<T>()) {
            return defaultValue;
        }
        return v.value<T>();
    }
};

} // namespace mf::core::interfaces
