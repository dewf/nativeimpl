#pragma once

#include "Implementation.h"

class ClientThing01;
class ServerThing01;

class NegativeException {
public:
    int value;
    NegativeException(int value) : value(value) {}
    void push() const {
        ni_pushInt(value);
    }
    static void buildAndThrow() {
        throw NegativeException(ni_popInt());
    }
};

class InterfaceMethodException {
public:
    int value;
    InterfaceMethodException(int value) : value(value) {}
    void push() const {
        ni_pushInt(value);
    }
    static void buildAndThrow() {
        throw InterfaceMethodException(ni_popInt());
    }
};

extern ni_InterfaceMethodRef ithing_someMethod;
extern ni_InterfaceMethodRef ithing_stashThing;
extern ni_InterfaceMethodRef ithing_getStashedThing;
extern ni_InterfaceMethodRef ithing_testThrow;
extern ni_InterfaceMethodRef ithing_onTimerTick;
extern ni_ExceptionRef negativeException;
extern ni_ExceptionRef interfaceMethodException;

std::shared_ptr<IThing01> IThing01__pop();
void IThing01__push(std::shared_ptr<IThing01> outer, bool isReturn);

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

// maybe we can simplify and get rid of ServerThing entirely? just use defaults on IThing01?
class ServerThing01 : public IThing01, public ServerObject {
public:
    virtual ~ServerThing01() {
        //printf("ServerThing01 dtor\n");
    }
    static std::shared_ptr<ServerThing01> getByID(int id) {
        auto obj = ServerObject::getByID(id);
        return std::static_pointer_cast<ServerThing01>(obj);
    }
};


