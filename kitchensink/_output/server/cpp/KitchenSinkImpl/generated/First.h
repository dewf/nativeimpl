#pragma once

#include "../support/NativeImplServer.h"
#include <functional>
#include <memory>
#include <string>
#include <vector>
#include <map>
#include <tuple>
#include <set>
#include <optional>
#include "../support/result.h"

namespace First
{
    void neatoMethod(std::vector<std::optional<int32_t>> maybeArray);
    std::vector<std::optional<int32_t>> returnsArray();
}
