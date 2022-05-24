#include "NativeImplServer.h"
#include "Implementation.h"

ni_InterfaceMethodRef coolThing_method1;

class ClientCoolThing : public ClientObject, public CoolThing {
public:
    ClientCoolThing(int id) : ClientObject(id) {}
    int32_t method1(std::string x) override {
        pushStringInternal(x);
        invokeMethod(coolThing_method1);
        return ni_popInt32();
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

std::shared_ptr<CoolThing> CoolThing__pop()
{
    bool isClientID, inSafetyArea;
    auto id = ni_popInstance(&isClientID, &inSafetyArea);
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

void doSomething__wrapper() {
    auto x = ni_popInt32();
    auto y = popStringInternal();
    ni_pushInt32(doSomething(x, y));
}

void CoolThing_method1__wrapper(int serverID) {
    auto inst = ServerCoolThing::getByID(serverID);
    auto x = popStringInternal();
    ni_pushInt32(inst->method1(x));
}

extern "C" int nativeLibraryInit() {
    auto m = ni_registerModule("TestLibrary");
    ni_registerModuleMethod(m, "doSomething", &doSomething__wrapper);
    auto coolThing = ni_registerInterface(m, "CoolThing");
    coolThing_method1 = ni_registerInterfaceMethod(coolThing, "method1", &CoolThing_method1__wrapper);
    return 0; // = OK
}

extern "C" void nativeLibraryShutdown() {
}
