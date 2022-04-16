#include "NativeImplServer.h"
#include <functional>
#include <memory>
#include <string>

typedef int IntFunc(int x);

class NegativeException {
public:
    int value;
    NegativeException(int value) : value(value) {}
    void push() const {
        ni_pushInt(value);
    }
    static void buildAndThrow() {
        auto _0 = ni_popInt();
        throw NegativeException(_0);
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
        auto _0 = ni_popInt();
        throw InterfaceMethodException(_0);
    }
};

class IThing01 {
public:
    virtual int someMethod(int x) = 0;
    virtual void stashThing(std::shared_ptr<IThing01> subThing) = 0;
    virtual std::shared_ptr<IThing01> getStashedThing() = 0;
    virtual void testThrow(bool doThrow) = 0;  // throws InterfaceMethodException
    virtual void onTimerTick() = 0;
};

class ServerIThing01 : public IThing01, public ServerObject {
public:
    virtual ~ServerIThing01() {
    }
    static std::shared_ptr<ServerIThing01> getByID(int id) {
        auto obj = ServerObject::getByID(id);
        return std::static_pointer_cast<ServerIThing01>(obj);
    }
};

extern const int global01;
extern const int anotherGlobal;
extern const int lastIntGlobal;

void stringTest(std::string str);
std::string returnString();
int sum(int a, int b, int c);
int execFunc01(std::function<IntFunc> func1);
void stashFunc(std::function<IntFunc> func2);
void releaseFunc();
std::function<IntFunc> returnStashedFunc();
std::shared_ptr<IThing01> makeThing();
int operateOnThing(std::shared_ptr<IThing01> thing, int arg);
void saveThing(std::shared_ptr<IThing01> thing);
std::shared_ptr<IThing01> getSavedThing();
void clientStashTest(std::shared_ptr<IThing01> thing);
void testNegative(int value);  // throws NegativeException
void testClientThrow(std::shared_ptr<IThing01> thing);
void startTimer(std::shared_ptr<IThing01> thing, int frequency);
void stopTimer();
