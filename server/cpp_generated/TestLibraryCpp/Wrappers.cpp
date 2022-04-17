#include "NativeImplServer.h"
#include "Implementation.h"

ni_InterfaceMethodRef heyas_someMethod;
ni_ExceptionRef coolException;

void CoolDelegate__push(std::function<CoolDelegate> f) {
    size_t uniqueKey = 0;
    if (f) {
        CoolDelegate* ptr_fun = f.target<CoolDelegate>();
        if (ptr_fun != nullptr) {
            uniqueKey = (size_t)ptr_fun;
        }
    }
    auto wrapper = [f]() {
        auto x = popStringInternal();
        ni_pushInt(f(x));
    };
    pushServerFuncVal(wrapper, uniqueKey);
}

std::function<CoolDelegate> CoolDelegate__pop() {
    auto id = ni_popClientFunc();
    auto cf = std::shared_ptr<ClientFunc>(new ClientFunc(id));
    auto wrapper = [cf](std::string x) {
        pushStringInternal(x);
        cf->remoteExec();
        return ni_popInt();
    };
    return wrapper;
}

void FinalDelegate__push(std::function<FinalDelegate> f) {
    size_t uniqueKey = 0;
    if (f) {
        FinalDelegate* ptr_fun = f.target<FinalDelegate>();
        if (ptr_fun != nullptr) {
            uniqueKey = (size_t)ptr_fun;
        }
    }
    auto wrapper = [f]() {
        (not yet implemented)
        f(v);
    };
    pushServerFuncVal(wrapper, uniqueKey);
}

std::function<FinalDelegate> FinalDelegate__pop() {
    auto id = ni_popClientFunc();
    auto cf = std::shared_ptr<ClientFunc>(new ClientFunc(id));
    auto wrapper = [cf](DeepMap v) {
        (pushValue not supported yet)
        cf->remoteExec();
    };
    return wrapper;
}

void Heyas__push(std::shared_ptr<Heyas> thing, bool isReturn);
std::shared_ptr<Heyas> Heyas__pop();

class ClientHeyas : public ClientObject, public Heyas {
public:
    ClientHeyas(int id) : ClientObject(id) {}
    void someMethod(int x) override {
        ni_pushInt(x);
        invokeMethodWithExceptions(heyas_someMethod);
    }
};

inline void Heyas__push(std::shared_ptr<Heyas> inst, bool isReturn) {
    auto pushable = std::dynamic_pointer_cast<Pushable>(inst);
    pushable->push(pushable, isReturn);
}

std::shared_ptr<Heyas> Heyas__pop()
{
    bool isClientID;
    auto id = ni_popInstance(&isClientID);
    if (isClientID) {
        return std::shared_ptr<Heyas>(new ClientHeyas(id));
    }
    else {
        return ServerHeyas::getByID(id);
    }
}

void mapTest__wrapper() {
    auto whoa = FinalDelegate__pop();
    mapTest(whoa);
}

void Heyas_someMethod__wrapper(int serverID) {
    auto inst = ServerHeyas::getByID(serverID);
    auto x = ni_popInt();
    try {
        inst->someMethod(x);
    }
    catch (const CoolException& e) {
        ni_setException(coolException);
        e.push();
    }
}

extern "C" int nativeLibraryInit() {
    auto m = ni_registerModule("TestLibrary");
    ni_registerModuleMethod(m, "mapTest", &mapTest__wrapper);
    auto heyas = ni_registerInterface(m, "Heyas");
    heyas_someMethod = ni_registerInterfaceMethod(heyas, "someMethod", &Heyas_someMethod__wrapper);
    coolException = ni_registerException(m, "CoolException", &CoolException::buildAndThrow);
    return 0; // = OK
}

extern "C" void nativeLibraryShutdown() {
}
