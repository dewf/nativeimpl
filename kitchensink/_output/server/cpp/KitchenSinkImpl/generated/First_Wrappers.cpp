#include "../support/NativeImplServer.h"
#include "First_wrappers.h"
#include "First.h"

namespace First
{
    ni_InterfaceMethodRef coolThing_doSomething;
    ni_ExceptionRef wootProblem;

    void WootProblem__push(WootProblem e) {
        pushStringInternal(e.s);
    }

    void WootProblem__buildAndThrow() {
        auto s = popStringInternal();
        throw WootProblem(s);
    }
    class ClientCoolThing : public ClientObject, public CoolThing {
    public:
        ClientCoolThing(int id) : ClientObject(id) {}
        void doSomething(int32_t x) override {
            ni_pushInt32(x);
            invokeMethodWithExceptions(coolThing_doSomething);
        }
    };

    void CoolThing__push(std::shared_ptr<CoolThing> inst, bool isReturn) {
        if (inst != nullptr) {
            auto pushable = std::dynamic_pointer_cast<Pushable>(inst);
            pushable->push(pushable, isReturn);
        }
        else {
            ni_pushNull();
        }
    }

    std::shared_ptr<CoolThing> CoolThing__pop() {
        bool isClientID;
        auto id = ni_popInstance(&isClientID);
        if (id != 0) {
            if (isClientID) {
                return std::shared_ptr<CoolThing>(new ClientCoolThing(id));
            }
            else {
                return ServerCoolThing::getByID(id);
            }
        }
        else {
            return std::shared_ptr<CoolThing>();
        }
    }

    void CoolThing_doSomething__wrapper(int serverID) {
        auto inst = ServerCoolThing::getByID(serverID);
        auto x = ni_popInt32();
        try {
            inst->doSomething(x);
        }
        catch (const WootProblem& e) {
            ni_setException(wootProblem);
            WootProblem__push(e);
        }
    }

    void createThing__wrapper() {
        CoolThing__push(createThing(), true);
    }

    int __register() {
        auto m = ni_registerModule("First");
        ni_registerModuleMethod(m, "createThing", &createThing__wrapper);
        auto coolThing = ni_registerInterface(m, "CoolThing");
        coolThing_doSomething = ni_registerInterfaceMethod(coolThing, "doSomething", &CoolThing_doSomething__wrapper);
        wootProblem = ni_registerException(m, "WootProblem", &WootProblem__buildAndThrow);
        return 0; // = OK
    }
}
