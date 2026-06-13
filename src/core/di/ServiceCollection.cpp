#include "ServiceCollection.h"

namespace mf::core::di {

bool ServiceCollection::contains(const std::type_index& key) const {
    return factories_.find(key) != factories_.end();
}

void ServiceCollection::clear() {
    factories_.clear();
}

} // namespace mf::core::di
