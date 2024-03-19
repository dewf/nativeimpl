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
    void method1(std::vector<result::Result<int32_t, std::string>> resultArray);
    std::vector<result::Result<int32_t, std::string>> method2();
}
