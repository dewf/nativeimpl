#include "NativeImplServer.h"
#include "Implementation.h"

ni_ExceptionRef negativeException;
ni_ExceptionRef interfaceMethodException;
ni_InterfaceMethodRef iThing01_someMethod;
ni_InterfaceMethodRef iThing01_stashThing;
ni_InterfaceMethodRef iThing01_getStashedThing;
ni_InterfaceMethodRef iThing01_testThrow;
ni_InterfaceMethodRef iThing01_onTimerTick;

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

void IThing01__push(std::shared_ptr<IThing01> thing, bool isReturn);
std::shared_ptr<IThing01> IThing01__pop();

class ClientIThing01 : public ClientObject, public IThing01 {
public:
    ClientIThing01(int id) : ClientObject(id) {}
    int someMethod(int x) override {
        ni_pushInt(x);
        invokeMethod(iThing01_someMethod);
        return ni_popInt();
    }
    void stashThing(std::shared_ptr<IThing01> subThing) override {
        IThing01__push(subThing, false);
        invokeMethod(iThing01_stashThing);
    }
    std::shared_ptr<IThing01> getStashedThing() override {
        invokeMethod(iThing01_getStashedThing);
        auto __ret = IThing01__pop();
        ni_clearClientSafetyArea();
        return __ret;
    }
    void testThrow(bool doThrow) override {
        ni_pushBool(doThrow);
        invokeMethodWithExceptions(iThing01_testThrow);
    }
    void onTimerTick() override {
        invokeMethod(iThing01_onTimerTick);
    }
};

inline void IThing01__push(std::shared_ptr<IThing01> inst, bool isReturn) {
    auto pushable = std::dynamic_pointer_cast<Pushable>(inst);
    pushable->push(pushable, isReturn);
}

std::shared_ptr<IThing01> IThing01__pop()
{
    bool isClientID;
    auto id = ni_popInstance(&isClientID);
    if (isClientID) {
        return std::shared_ptr<IThing01>(new ClientIThing01(id));
    }
    else {
        return ServerIThing01::getByID(id);
    }
}

void stringTest__wrapper() {
    auto str = popStringInternal();
    stringTest(str);
}

void returnString__wrapper() {
    pushStringInternal(returnString());
}

void sum__wrapper() {
    auto a = ni_popInt();
    auto b = ni_popInt();
    auto c = ni_popInt();
    ni_pushInt(sum(a, b, c));
}

void execFunc01__wrapper() {
    auto func1 = IntFunc__pop();
    ni_pushInt(execFunc01(func1));
}

void stashFunc__wrapper() {
    auto func2 = IntFunc__pop();
    stashFunc(func2);
}

void releaseFunc__wrapper() {
    releaseFunc();
}

void returnStashedFunc__wrapper() {
    IntFunc__push(returnStashedFunc());
}

void makeThing__wrapper() {
    IThing01__push(makeThing(), true);
}

void operateOnThing__wrapper() {
    auto thing = IThing01__pop();
    auto arg = ni_popInt();
    ni_pushInt(operateOnThing(thing, arg));
}

void saveThing__wrapper() {
    auto thing = IThing01__pop();
    saveThing(thing);
}

void getSavedThing__wrapper() {
    IThing01__push(getSavedThing(), true);
}

void clientStashTest__wrapper() {
    auto thing = IThing01__pop();
    clientStashTest(thing);
}

void testNegative__wrapper() {
    auto value = ni_popInt();
    try {
        testNegative(value);
    }
    catch (const NegativeException& e) {
        ni_setException(negativeException);
        e.push();
    }
}

void testClientThrow__wrapper() {
    auto thing = IThing01__pop();
    testClientThrow(thing);
}

void startTimer__wrapper() {
    auto thing = IThing01__pop();
    auto frequency = ni_popInt();
    startTimer(thing, frequency);
}

void stopTimer__wrapper() {
    stopTimer();
}

void IThing01_someMethod__wrapper(int serverID) {
    auto inst = ServerIThing01::getByID(serverID);
    auto x = ni_popInt();
    ni_pushInt(inst->someMethod(x));
}

void IThing01_stashThing__wrapper(int serverID) {
    auto inst = ServerIThing01::getByID(serverID);
    auto subThing = IThing01__pop();
    inst->stashThing(subThing);
}

void IThing01_getStashedThing__wrapper(int serverID) {
    auto inst = ServerIThing01::getByID(serverID);
    IThing01__push(inst->getStashedThing(), true);
}

void IThing01_testThrow__wrapper(int serverID) {
    auto inst = ServerIThing01::getByID(serverID);
    auto doThrow = ni_popBool();
    try {
        inst->testThrow(doThrow);
    }
    catch (const InterfaceMethodException& e) {
        ni_setException(interfaceMethodException);
        e.push();
    }
}

void IThing01_onTimerTick__wrapper(int serverID) {
    auto inst = ServerIThing01::getByID(serverID);
    inst->onTimerTick();
}

void __constantsFunc() {
    ni_pushInt(lastIntGlobal);
    ni_pushInt(anotherGlobal);
    ni_pushInt(global01);
}

extern "C" int nativeLibraryInit() {
    auto m = ni_registerModule("TestLibrary");
    ni_registerModuleConstants(m, &__constantsFunc);
    ni_registerModuleMethod(m, "stringTest", &stringTest__wrapper);
    ni_registerModuleMethod(m, "returnString", &returnString__wrapper);
    ni_registerModuleMethod(m, "sum", &sum__wrapper);
    ni_registerModuleMethod(m, "execFunc01", &execFunc01__wrapper);
    ni_registerModuleMethod(m, "stashFunc", &stashFunc__wrapper);
    ni_registerModuleMethod(m, "releaseFunc", &releaseFunc__wrapper);
    ni_registerModuleMethod(m, "returnStashedFunc", &returnStashedFunc__wrapper);
    ni_registerModuleMethod(m, "makeThing", &makeThing__wrapper);
    ni_registerModuleMethod(m, "operateOnThing", &operateOnThing__wrapper);
    ni_registerModuleMethod(m, "saveThing", &saveThing__wrapper);
    ni_registerModuleMethod(m, "getSavedThing", &getSavedThing__wrapper);
    ni_registerModuleMethod(m, "clientStashTest", &clientStashTest__wrapper);
    ni_registerModuleMethod(m, "testNegative", &testNegative__wrapper);
    ni_registerModuleMethod(m, "testClientThrow", &testClientThrow__wrapper);
    ni_registerModuleMethod(m, "startTimer", &startTimer__wrapper);
    ni_registerModuleMethod(m, "stopTimer", &stopTimer__wrapper);
    auto iThing01 = ni_registerInterface(m, "IThing01");
    iThing01_someMethod = ni_registerInterfaceMethod(iThing01, "someMethod", &IThing01_someMethod__wrapper);
    iThing01_stashThing = ni_registerInterfaceMethod(iThing01, "stashThing", &IThing01_stashThing__wrapper);
    iThing01_getStashedThing = ni_registerInterfaceMethod(iThing01, "getStashedThing", &IThing01_getStashedThing__wrapper);
    iThing01_testThrow = ni_registerInterfaceMethod(iThing01, "testThrow", &IThing01_testThrow__wrapper);
    iThing01_onTimerTick = ni_registerInterfaceMethod(iThing01, "onTimerTick", &IThing01_onTimerTick__wrapper);
    negativeException = ni_registerException(m, "NegativeException", &NegativeException::buildAndThrow);
    interfaceMethodException = ni_registerException(m, "InterfaceMethodException", &InterfaceMethodException::buildAndThrow);
    return 0; // = OK
}

extern "C" void nativeLibraryShutdown() {
}
