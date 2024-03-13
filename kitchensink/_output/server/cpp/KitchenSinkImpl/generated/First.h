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
    namespace InterfaceGroup
    {
        class MyInterface {
        public:
            virtual void doesSomething(std::string s) = 0;
        };

        // inherit from this to create server instances of MyInterface
        class ServerMyInterface : public MyInterface, public ServerObject {
        public:
            virtual ~ServerMyInterface() {}
            static std::shared_ptr<ServerMyInterface> getByID(int id) {
                auto obj = ServerObject::getByID(id);
                return std::static_pointer_cast<ServerMyInterface>(obj);
            }
        };
    }
    void operateOnInterface(std::shared_ptr<InterfaceGroup::MyInterface> thing);
}
