#include "../support/NativeImplServer.h"
#include "First_wrappers.h"
#include "First.h"

namespace First
{
    ni_InterfaceMethodRef whateverThing_someMethod;
    ni_ExceptionRef coolException;
    class ClientWhateverThing : public ClientObject, public WhateverThing {
    public:
        ClientWhateverThing(int id) : ClientObject(id) {}
        int32_t someMethod(std::string s) override {
            pushStringInternal(s);
            invokeMethod(whateverThing_someMethod);
            return ni_popInt32();
        }
    };

    void WhateverThing__push(std::shared_ptr<WhateverThing> inst, bool isReturn) {
        if (inst != nullptr) {
            auto pushable = std::dynamic_pointer_cast<Pushable>(inst);
            pushable->push(pushable, isReturn);
        }
        else {
            ni_pushNull();
        }
    }

    std::shared_ptr<WhateverThing> WhateverThing__pop() {
        bool isClientID;
        auto id = ni_popInstance(&isClientID);
        if (id != 0) {
            if (isClientID) {
                return std::shared_ptr<WhateverThing>(new ClientWhateverThing(id));
            }
            else {
                return ServerWhateverThing::getByID(id);
            }
        }
        else {
            return std::shared_ptr<WhateverThing>();
        }
    }

    void WhateverThing_someMethod__wrapper(int serverID) {
        auto inst = ServerWhateverThing::getByID(serverID);
        auto s = popStringInternal();
        ni_pushInt32(inst->someMethod(s));
    }

    void operateOnThing__wrapper() {
        auto thing = WhateverThing__pop();
        operateOnThing(thing);
    }

    void doSomething__wrapper() {
        auto a = ni_popInt32();
        auto b = ni_popInt32();
        pushStringInternal(doSomething(a, b));
    }

    void CoolException__push(CoolException e) {
        pushStringInternal(e.reason);
    }

    void CoolException__buildAndThrow() {
        auto reason = popStringInternal();
        throw CoolException(reason);
    }

    void throwException__wrapper() {
        auto content = popStringInternal();
        try {
            throwException(content);
        }
        catch (const CoolException& e) {
            ni_setException(coolException);
            CoolException__push(e);
        }
    }

    int __register() {
        auto m = ni_registerModule("First");
        ni_registerModuleMethod(m, "operateOnThing", &operateOnThing__wrapper);
        ni_registerModuleMethod(m, "doSomething", &doSomething__wrapper);
        ni_registerModuleMethod(m, "throwException", &throwException__wrapper);
        auto whateverThing = ni_registerInterface(m, "WhateverThing");
        whateverThing_someMethod = ni_registerInterfaceMethod(whateverThing, "someMethod", &WhateverThing_someMethod__wrapper);
        coolException = ni_registerException(m, "CoolException", &CoolException__buildAndThrow);
        return 0; // = OK
    }
}
