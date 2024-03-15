#include "../support/NativeImplServer.h"
#include "First_wrappers.h"
#include "First.h"

namespace First
{
    ni_InterfaceMethodRef iTestInterface_wootMethod;
    class ClientITestInterface : public ClientObject, public ITestInterface {
    public:
        ClientITestInterface(int id) : ClientObject(id) {}
        void wootMethod(int32_t x) override {
            ni_pushInt32(x);
            invokeMethod(iTestInterface_wootMethod);
        }
    };

    void ITestInterface__push(std::shared_ptr<ITestInterface> inst, bool isReturn) {
        if (inst != nullptr) {
            auto pushable = std::dynamic_pointer_cast<Pushable>(inst);
            pushable->push(pushable, isReturn);
        }
        else {
            ni_pushNull();
        }
    }

    std::shared_ptr<ITestInterface> ITestInterface__pop() {
        bool isClientID;
        auto id = ni_popInstance(&isClientID);
        if (id != 0) {
            if (isClientID) {
                return std::shared_ptr<ITestInterface>(new ClientITestInterface(id));
            }
            else {
                return ServerITestInterface::getByID(id);
            }
        }
        else {
            return std::shared_ptr<ITestInterface>();
        }
    }

    void ITestInterface_wootMethod__wrapper(int serverID) {
        auto inst = ServerITestInterface::getByID(serverID);
        auto x = ni_popInt32();
        inst->wootMethod(x);
    }

    void operateOnInterface__wrapper() {
        auto thing = ITestInterface__pop();
        operateOnInterface(thing);
    }

    int __register() {
        auto m = ni_registerModule("First");
        ni_registerModuleMethod(m, "operateOnInterface", &operateOnInterface__wrapper);
        auto iTestInterface = ni_registerInterface(m, "ITestInterface");
        iTestInterface_wootMethod = ni_registerInterfaceMethod(iTestInterface, "wootMethod", &ITestInterface_wootMethod__wrapper);
        return 0; // = OK
    }
}
