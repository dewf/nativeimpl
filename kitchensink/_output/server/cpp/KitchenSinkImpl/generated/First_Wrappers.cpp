#include "../support/NativeImplServer.h"
#include "First_wrappers.h"
#include "First.h"

namespace First
{
    namespace InterfaceGroup
    {
        ni_InterfaceMethodRef myInterface_doesSomething;
        class ClientMyInterface : public ClientObject, public MyInterface {
        public:
            ClientMyInterface(int id) : ClientObject(id) {}
            void doesSomething(std::string s) override {
                pushStringInternal(s);
                invokeMethod(myInterface_doesSomething);
            }
        };

        void MyInterface__push(std::shared_ptr<MyInterface> inst, bool isReturn) {
            if (inst != nullptr) {
                auto pushable = std::dynamic_pointer_cast<Pushable>(inst);
                pushable->push(pushable, isReturn);
            }
            else {
                ni_pushNull();
            }
        }

        std::shared_ptr<MyInterface> MyInterface__pop() {
            bool isClientID;
            auto id = ni_popInstance(&isClientID);
            if (id != 0) {
                if (isClientID) {
                    return std::shared_ptr<MyInterface>(new ClientMyInterface(id));
                }
                else {
                    return ServerMyInterface::getByID(id);
                }
            }
            else {
                return std::shared_ptr<MyInterface>();
            }
        }

        void MyInterface_doesSomething__wrapper(int serverID) {
            auto inst = ServerMyInterface::getByID(serverID);
            auto s = popStringInternal();
            inst->doesSomething(s);
        }
    }

    void operateOnInterface__wrapper() {
        auto thing = InterfaceGroup::MyInterface__pop();
        operateOnInterface(thing);
    }

    int __register() {
        auto m = ni_registerModule("First");
        ni_registerModuleMethod(m, "operateOnInterface", &operateOnInterface__wrapper);
        auto myInterface = ni_registerInterface(m, "InterfaceGroup.MyInterface");
        InterfaceGroup::myInterface_doesSomething = ni_registerInterfaceMethod(myInterface, "doesSomething", &InterfaceGroup::MyInterface_doesSomething__wrapper);
        return 0; // = OK
    }
}
