#include "../support/NativeImplServer.h"
#include "First_wrappers.h"
#include "First.h"

namespace First
{
    ni_InterfaceMethodRef iTestInterface_wootMethod;

    // a map to allow users to use bare interfaces, instead of having
    // to inherit from something with all the Pushable implementation
    // a bit indirect but makes development that much more obvious and direct,
    // which is the point of NativeImpl!
    static std::map<ITestInterface*, std::weak_ptr<Pushable>> __iTestInterfaceToPushable;

    class ServerITestInterfaceWrapper : public ServerObject {
    public:
        std::shared_ptr<ITestInterface> rawInterface;
    private:
        ServerITestInterfaceWrapper(std::shared_ptr<ITestInterface> raw) {
            this->rawInterface = raw;
        }
        void releaseExtra() override {
            // remove entry from map, remote side released us
            __iTestInterfaceToPushable.erase(rawInterface.get());
        }
    public:
        static std::shared_ptr<ServerITestInterfaceWrapper> wrapAndRegister(std::shared_ptr<ITestInterface> raw) {
            auto ret = std::shared_ptr<ServerITestInterfaceWrapper>(new ServerITestInterfaceWrapper(raw));
            __iTestInterfaceToPushable[raw.get()] = ret;
            return ret;
        }
    };

    class ClientITestInterface : public ClientObject, public ITestInterface {
    public:
        ClientITestInterface(int id) : ClientObject(id) {}
        ~ClientITestInterface() override {
             __iTestInterfaceToPushable.erase(this);
        }
        void wootMethod(int32_t x) override {
            ni_pushInt32(x);
            invokeMethod(iTestInterface_wootMethod);
        }
    };

    void ITestInterface__push(std::shared_ptr<ITestInterface> inst, bool isReturn) {
        if (inst != nullptr) {
            auto found = __iTestInterfaceToPushable.find(inst.get());
            if (found != __iTestInterfaceToPushable.end()) {
                // if it's already in the table (client thing, or as-yet-unknown server thing),
                // retrieve and push directly
                auto pushable = found->second.lock();
                pushable->push(pushable, isReturn);
            }
            else {
                // if it's not in the table, it's a new server thing, needs to be wrapped and pushed
                auto pushable = ServerITestInterfaceWrapper::wrapAndRegister(inst);
                pushable->push(pushable, isReturn);
            }
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
                auto ret = std::shared_ptr<ITestInterface>(new ClientITestInterface(id));
                // store incoming client things so we can look them up by interface
                __iTestInterfaceToPushable[ret.get()] = std::dynamic_pointer_cast<Pushable>(ret);
                return ret;
            }
            else {
                // getting back something we previously sent over, so we know a wrapper already exists
                auto wrapper = std::static_pointer_cast<ServerITestInterfaceWrapper>(ServerObject::getByID(id));
                return wrapper->rawInterface;
            }
        }
        else {
            return std::shared_ptr<ITestInterface>();
        }
    }

    void ITestInterface_wootMethod__wrapper(int serverID) {
        auto wrapper = std::static_pointer_cast<ServerITestInterfaceWrapper>(ServerObject::getByID(serverID));
        auto inst = wrapper->rawInterface;
        auto x = ni_popInt32();
        inst->wootMethod(x);
    }

    void operateOnInterface__wrapper() {
        auto thing = ITestInterface__pop();
        operateOnInterface(thing);
    }

    void serverCreate__wrapper() {
        ITestInterface__push(serverCreate(), true);
    }

    int __register() {
        auto m = ni_registerModule("First");
        ni_registerModuleMethod(m, "operateOnInterface", &operateOnInterface__wrapper);
        ni_registerModuleMethod(m, "serverCreate", &serverCreate__wrapper);
        auto iTestInterface = ni_registerInterface(m, "ITestInterface");
        iTestInterface_wootMethod = ni_registerInterfaceMethod(iTestInterface, "wootMethod", &ITestInterface_wootMethod__wrapper);
        return 0; // = OK
    }
}
