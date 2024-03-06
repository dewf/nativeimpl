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

    class WhateverThing {
    public:
        virtual int32_t someMethod(std::string s) = 0;
    };

    // inherit from this to create server instances of WhateverThing
    class ServerWhateverThing : public WhateverThing, public ServerObject {
    public:
        virtual ~ServerWhateverThing() {}
        static std::shared_ptr<ServerWhateverThing> getByID(int id) {
            auto obj = ServerObject::getByID(id);
            return std::static_pointer_cast<ServerWhateverThing>(obj);
        }
    };
    void operateOnThing(std::shared_ptr<WhateverThing> thing);
    std::string doSomething(int32_t a, int32_t b);

    class CoolException {
    public:
        std::string reason;
        CoolException(std::string reason)
            : reason(reason) {}
    };
    void throwException(std::string content); // throws CoolException
}
