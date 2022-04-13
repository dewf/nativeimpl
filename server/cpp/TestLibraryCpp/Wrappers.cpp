#include "NativeImplServer.h"

#include "Implementation.h"

ni_InterfaceMethodRef ithing_someMethod;
ni_InterfaceMethodRef ithing_stashThing;
ni_InterfaceMethodRef ithing_getStashedThing;
ni_InterfaceMethodRef ithing_testThrow;
ni_InterfaceMethodRef ithing_onTimerTick;
ni_ExceptionRef negativeException;
ni_ExceptionRef interfaceMethodException;

// fwd decls for client wrapper
void IThing01__push(std::shared_ptr<IThing01> thing, bool isReturn);
std::shared_ptr<IThing01> IThing01__pop();

class ClientThing01 : public ClientObject, public IThing01 {
public:
    ClientThing01(int id) : ClientObject(id) {}
    int someMethod(int x) override {
        ni_pushInt(x);
        invokeMethod(ithing_someMethod); // invoke remote, woot
        return ni_popInt();
    }
    void stashThing(std::shared_ptr<IThing01> subThing) override {
        IThing01__push(subThing, false);
        invokeMethod(ithing_stashThing);
    }
    std::shared_ptr<IThing01> getStashedThing() override {
        printf("C++ ClientThing01::getStashedthing()\n");
        invokeMethod(ithing_getStashedThing);
        auto ret = IThing01__pop();
        printf("== C++ before clearClientSafetyArea ====\n");
        ni_clearClientSafetyArea(); // it can do whatever with those now
        printf("== C++ after clearClientSafetyArea ====\n");
        return ret;
    }
    void testThrow(bool doThrow) override {
        ni_pushBool(doThrow);
        invokeMethodWithExceptions(ithing_testThrow);
    }
    void onTimerTick() override {
        invokeMethod(ithing_onTimerTick);
    }
};

std::shared_ptr<IThing01> IThing01__pop()
{
    bool isClientID;
    auto id = ni_popInstance(&isClientID);
    if (isClientID) {
        return std::shared_ptr<IThing01>(new ClientThing01(id));
    }
    else {
        return ServerThing01::getByID(id);
    }
}

void IThing01__push(std::shared_ptr<IThing01> thing, bool isReturn) {
    // we know this to be a pushable because you shouldn't be creating anything but ServerIThing01's (perhaps rename/alias to BaseIThing01?)
    auto pushable = std::dynamic_pointer_cast<Pushable>(thing);
    pushable->push(pushable, isReturn);
}

// ==============================================================
// library wrapper stuff

void sumWrapper() {
    int a = ni_popInt();
    int b = ni_popInt();
    int c = ni_popInt();
    ni_pushInt(sum(a, b, c));
}

void execFunc01Wrapper() {
    auto id = ni_popClientFunc();
    auto cf = std::shared_ptr<ClientFunc>(new ClientFunc(id)); // wrap it in something that knows how to invoke / clean up that remote function
    auto func = [cf](int x) {
        ni_pushInt(x);
        cf->remoteExec();
        return ni_popInt();
        // when this lambda is destroyed, so will cf which will trigger the cleanup/release of remote function
    };
    ni_pushInt(execFunc01(func));
}

void stashFuncWrapper() {
    auto id = ni_popClientFunc();
    auto cf = std::shared_ptr<ClientFunc>(new ClientFunc(id));
    auto func = [cf](int x) {
        ni_pushInt(x);
        cf->remoteExec();
        return ni_popInt();
    };
    stashFunc(func);
}

void releaseFuncWrapper() {
    releaseFunc();
}

void returnStashedFuncWrapper() {
    // note this always wraps with an indirect/wrapped server func - no short-circuiting possible with function values going across client/server boundary
    // (because no way to pull apart a std::function to see what's inside, distinguishing client/server funcs)
    // ie, if for some reason you're sending and returning functions across the boundary,
    // it will keep being wrapped over and over again! needless to say, very bad for performance :)
    // if you need that kind of behavior, use a single-method interface instead - will properly be short-circuited across the boundary

    // get the native function value
    auto f = returnStashedFunc();

    // an attempt to not needlessly duplicate simple functions in our function table (won't work for closures, right?)
    size_t uniqueKey = 0;
    if (f) {
        typedef int function_t(int);
        function_t* ptr_fun = f.target<function_t>();
        if (ptr_fun != nullptr) {
            uniqueKey = (size_t)ptr_fun;
        }
    }

    // push the id for the wrapper of that function on the stack
    // client side will be able to invoke this wrapper whenever
    // ideally this would de-duplicate (for unique values of f) but no idea how to do that in C++ :(
    auto wrapper = [f]() {
        auto x = ni_popInt();
        auto res = f(x);
        ni_pushInt(res);
    };
    pushServerFuncVal(wrapper, uniqueKey);
}

void makeThingWrapper() {
    auto thing = makeThing();
    IThing01__push(thing, true);
}

void operateOnThingWrapper() {
    auto thing = IThing01__pop(); // could be either client or server object!
    auto arg = ni_popInt();
    auto res = operateOnThing(thing, arg);
    ni_pushInt(res);
}

// ithing_(name) methods are ONLY called on server objects, the remote side can invoke its own methods directly!

void ithing_someMethodWrapper(int serverID) {
    auto inst = ServerThing01::getByID(serverID);
    auto arg = ni_popInt();
    auto res = inst->someMethod(arg);
    ni_pushInt(res);
}

void ithing_stashThingWrapper(int serverID) {
    auto inst = ServerThing01::getByID(serverID);
    auto arg = IThing01__pop();
    inst->stashThing(arg);
}

void ithing_getStashedThingWrapper(int serverID) {
    auto inst = ServerThing01::getByID(serverID);
    auto ret = inst->getStashedThing();
    IThing01__push(ret, true); // safe push - remote will need to clear safe area
}

void ithing_testThrowWrapper(int serverID) {
    auto inst = ServerThing01::getByID(serverID);
    auto doThrow = ni_popBool();
    try {
        inst->testThrow(doThrow);
    }
    catch (const InterfaceMethodException& e) {
        ni_setException(interfaceMethodException);
        e.push();
    }
}

void ithing_onTimerTickWrapper(int serverID) {
    auto inst = ServerThing01::getByID(serverID);
    inst->onTimerTick();
}

void saveThingWrapper() {
    auto thing = IThing01__pop();
    saveThing(thing);
}

void getSavedThingWrapper() {
    auto thing = getSavedThing();
    IThing01__push(thing, true);
}

void clientStashTestWrapper() {
    auto thing = IThing01__pop(); // (probably) client thing
    clientStashTest(thing); // internally will test that interface
}

void constantsFunc() {
    // push in reverse order
    ni_pushInt(lastIntGlobal);
    ni_pushInt(anotherGlobal);
    ni_pushInt(global01);
}

void testNegativeWrapper() {
    auto arg = ni_popInt();
    try {
        testNegative(arg);
    }
    catch (const NegativeException& e) {
        ni_setException(negativeException);
        e.push();
    }
}

void testClientThrowWrapper() {
    auto thing = IThing01__pop(); // probably client thing
    testClientThrow(thing);
}

void startTimerWrapper() {
    auto thing = IThing01__pop();
    auto freq = ni_popInt();
    startTimer(thing, freq);
}

void stopTimerWrapper() {
    stopTimer();
}


// ====================================================================
// library entry point / registrations

extern "C" int nativeLibraryInit() {
    printf("hello from DLL initLibrary()\n");

    auto m = ni_registerModule("TestLibrary");

    // module constants
    ni_registerModuleConstants(m, &constantsFunc);

    // module methods
    ni_registerModuleMethod(m, "sum|iii", &sumWrapper);
    ni_registerModuleMethod(m, "execFunc01|(i->i)", &execFunc01Wrapper);
    ni_registerModuleMethod(m, "stashFunc|(i->i)", &stashFuncWrapper);
    ni_registerModuleMethod(m, "releaseFunc|", &releaseFuncWrapper);
    ni_registerModuleMethod(m, "returnStashedFunc|", &returnStashedFuncWrapper);
    ni_registerModuleMethod(m, "makeThing|", &makeThingWrapper);
    ni_registerModuleMethod(m, "operateOnThing|(IThing01)i", &operateOnThingWrapper);
    ni_registerModuleMethod(m, "saveThing|(IThing01)", &saveThingWrapper);
    ni_registerModuleMethod(m, "getSavedThing|", &getSavedThingWrapper);
    ni_registerModuleMethod(m, "clientStashTest|(IThing01)", &clientStashTestWrapper);
    ni_registerModuleMethod(m, "testNegative|i", &testNegativeWrapper);
    ni_registerModuleMethod(m, "testClientThrow|(IThing01)", &testClientThrowWrapper);
    ni_registerModuleMethod(m, "startTimer|(IThing01)i", &startTimerWrapper);
    ni_registerModuleMethod(m, "stopTimer|", &stopTimerWrapper);

    // IThing01 stuff
    auto ithing = ni_registerInterface(m, "IThing01");
    // note the following is stored globally:
    ithing_someMethod = ni_registerInterfaceMethod(ithing, "someMethod|i", &ithing_someMethodWrapper);
    ithing_stashThing = ni_registerInterfaceMethod(ithing, "stashThing|(IThing01)", &ithing_stashThingWrapper);
    ithing_getStashedThing = ni_registerInterfaceMethod(ithing, "getStashedThing|", &ithing_getStashedThingWrapper);
    ithing_testThrow = ni_registerInterfaceMethod(ithing, "testThrow|b", &ithing_testThrowWrapper);
    ithing_onTimerTick = ni_registerInterfaceMethod(ithing, "onTimerTick|", &ithing_onTimerTickWrapper);

    // exceptions
    negativeException = ni_registerException(m, "NegativeException", &NegativeException::buildAndThrow);
    interfaceMethodException = ni_registerException(m, "InterfaceMethodException", &InterfaceMethodException::buildAndThrow);

    fflush(stdout); // for JNI
    return 0; // 0 = OK
}

extern "C" void nativeLibraryShutdown() {
    printf("C++ nativeLibraryShutdown()");
    // nothing for C++
}
