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

    // inherit from this to create server instances of ITestInterface
    class ServerITestInterface : public ITestInterface, public ServerObject {
    public:
        virtual ~ServerITestInterface() {}
        static std::shared_ptr<ServerITestInterface> getByID(int id) {
            auto obj = ServerObject::getByID(id);
            return std::static_pointer_cast<ServerITestInterface>(obj);
        }
    };
    void operateOnInterface(std::shared_ptr<ITestInterface> thing);
    std::shared_ptr<ITestInterface> serverCreate();
}
