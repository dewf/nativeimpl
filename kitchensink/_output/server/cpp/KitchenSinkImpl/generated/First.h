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

    class WootProblem {
    public:
        std::string s;
        WootProblem(std::string s)
            : s(s) {}
    };

    class CoolThing {
    public:
        virtual void doSomething(int32_t x) = 0; // throws WootProblem
    };

    // inherit from this to create server instances of CoolThing
    class ServerCoolThing : public CoolThing, public ServerObject {
    public:
        virtual ~ServerCoolThing() {}
        static std::shared_ptr<ServerCoolThing> getByID(int id) {
            auto obj = ServerObject::getByID(id);
            return std::static_pointer_cast<ServerCoolThing>(obj);
        }
    };
    std::shared_ptr<CoolThing> createThing();
}
