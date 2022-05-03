#include "NativeImplServer.h"
#include "Implementation.h"

ni_InterfaceMethodRef myThing_doSomething;

void IntFunc__push(std::function<IntFunc> f) {
    size_t uniqueKey = 0;
    if (f) {
        IntFunc* ptr_fun = f.target<IntFunc>();
        if (ptr_fun != nullptr) {
            uniqueKey = (size_t)ptr_fun;
        }
    }
    auto wrapper = [f]() {
        auto x = ni_popInt();
        ni_pushInt(f(x));
    };
    pushServerFuncVal(wrapper, uniqueKey);
}

std::function<IntFunc> IntFunc__pop() {
    auto id = ni_popClientFunc();
    auto cf = std::shared_ptr<ClientFunc>(new ClientFunc(id));
    auto wrapper = [cf](int x) {
        ni_pushInt(x);
        cf->remoteExec();
        return ni_popInt();
    };
    return wrapper;
}

class ClientMyThing : public ClientObject, public MyThing {
public:
    ClientMyThing(int id) : ClientObject(id) {}
    int doSomething(int x) override {
        ni_pushInt(x);
        invokeMethod(myThing_doSomething);
        return ni_popInt();
    }
};

inline void MyThing__push(std::shared_ptr<MyThing> inst, bool isReturn) {
    auto pushable = std::dynamic_pointer_cast<Pushable>(inst);
    pushable->push(pushable, isReturn);
}

std::shared_ptr<MyThing> MyThing__pop()
{
    bool isClientID, inSafetyArea;
    auto id = ni_popInstance(&isClientID, &inSafetyArea);
    if (isClientID) {
        return std::shared_ptr<MyThing>(new ClientMyThing(id));
    }
    else {
        return ServerMyThing::getByID(id);
    }
}

void takesThing__wrapper() {
    auto thing = MyThing__pop();
    takesThing(thing);
}

void returnsThing__wrapper() {
    MyThing__push(returnsThing(), true);
}

void applyFunc__wrapper() {
    auto f = IntFunc__pop();
    applyFunc(f);
}

void MyThing_doSomething__wrapper(int serverID) {
    auto inst = ServerMyThing::getByID(serverID);
    auto x = ni_popInt();
    ni_pushInt(inst->doSomething(x));
}

extern "C" int nativeLibraryInit() {
    auto m = ni_registerModule("TestLibrary");
    ni_registerModuleMethod(m, "takesThing", &takesThing__wrapper);
    ni_registerModuleMethod(m, "returnsThing", &returnsThing__wrapper);
    ni_registerModuleMethod(m, "applyFunc", &applyFunc__wrapper);
    auto myThing = ni_registerInterface(m, "MyThing");
    myThing_doSomething = ni_registerInterfaceMethod(myThing, "doSomething", &MyThing_doSomething__wrapper);
    return 0; // = OK
}

extern "C" void nativeLibraryShutdown() {
}
