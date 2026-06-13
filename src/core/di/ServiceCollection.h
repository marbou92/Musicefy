#pragma once

#include <QString>

#include <any>
#include <functional>
#include <memory>
#include <typeindex>
#include <unordered_map>
#include <vector>

namespace mf::core::di {

class ServiceCollection {
public:
    enum class Lifetime { Singleton, Transient };

    template <typename TInterface, typename TImpl = TInterface, typename... Args>
    ServiceCollection& registerType(Lifetime lifetime = Lifetime::Singleton) {
        auto key = std::type_index(typeid(TInterface));
        auto factory = [lifetime]() -> std::any {
            if (lifetime == Lifetime::Singleton) {
                static std::any instance;
                if (!instance.has_value()) {
                    instance = std::make_shared<TImpl>();
                }
                return instance;
            }
            return std::make_shared<TImpl>();
        };
        factories_[key] = factory;
        return *this;
    }

    template <typename TInterface, typename TImpl>
    ServiceCollection& registerFactory(std::function<std::shared_ptr<TImpl>()> factory) {
        auto key = std::type_index(typeid(TInterface));
        factories_[key] = [factory]() -> std::any {
            return factory();
        };
        return *this;
    }

    template <typename T>
    std::shared_ptr<T> resolve() const {
        auto key = std::type_index(typeid(T));
        auto it = factories_.find(key);
        if (it == factories_.end()) {
            return nullptr;
        }
        return std::any_cast<std::shared_ptr<T>>(it->second());
    }

    bool contains(const std::type_index& key) const;
    void clear();

private:
    std::unordered_map<std::type_index, std::function<std::any()>> factories_;
};

} // namespace mf::core::di
