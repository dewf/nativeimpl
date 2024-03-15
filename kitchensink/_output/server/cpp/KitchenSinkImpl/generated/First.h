#pragma once

#include "../support/NativeImplServer.h"
#include <functional>
#include <memory>
#include <string>
#include <vector>
#include <map>
#include <tuple>
#include <set>

namespace First
{

    class ITestInterface {
    public:
        virtual void wootMethod(int32_t x) = 0;
    };

    void operateOnInterface(std::shared_ptr<ITestInterface> thing);
    std::shared_ptr<ITestInterface> serverCreate();
}
