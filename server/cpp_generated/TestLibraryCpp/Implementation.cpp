#include "Implementation.h"
#include <memory>
#include <functional>

const int global01 = 0;
const int anotherGlobal = 0;
const int lastIntGlobal = 0;

void stringTest(std::string str) {
    printf("generated C++ stringTest: [%s]\n", str.c_str());
}

std::string returnString() {
    return "C++ string being returned!!";
}

int sum(int a, int b, int c) {
    return a + b + c;
}

int execFunc01(std::function<IntFunc> func1) {
    return func1(1001);
}

int defaultStashedFunc(int x) {
    return x * 100;
}

std::function<IntFunc> stashed = defaultStashedFunc;

void stashFunc(std::function<IntFunc> func2) {
    stashed = func2;
}

void releaseFunc() {
    stashed = defaultStashedFunc;
}

std::function<IntFunc> returnStashedFunc() {
    return stashed;
}


class MyThing : public ServerIThing01 {
private:
    std::shared_ptr<IThing01> myThing;
public:
    int someMethod(int x) override {
        return x + 456;
    }
    void stashThing(std::shared_ptr<IThing01> subThing) override {
        myThing = subThing;
    }
    std::shared_ptr<IThing01> getStashedThing() override {
        return myThing;
    }
    void testThrow(bool doThrow) override {
        if (doThrow) {
            throw InterfaceMethodException(111);
        }
    }
    void onTimerTick() override {
        printf("hello from C++ MyThing.onTimerTick()\n");
    }
};

std::shared_ptr<IThing01> makeThing() {
    return std::shared_ptr<IThing01>(new MyThing());
}

int operateOnThing(std::shared_ptr<IThing01> thing, int arg) {
    return thing->someMethod(arg + 1);
}

static std::shared_ptr<IThing01> savedThing;

void saveThing(std::shared_ptr<IThing01> thing) {
    savedThing = thing;
}

std::shared_ptr<IThing01> getSavedThing() {
    return savedThing;
}

void clientStashTest(std::shared_ptr<IThing01> thing) {
    auto toStore = std::shared_ptr<IThing01>(new MyThing());
    thing->stashThing(toStore);
}

void testNegative(int value) {  // throws NegativeException
    if (value < 0) {
        throw NegativeException(value);
    }
}

void testClientThrow(std::shared_ptr<IThing01> thing) {
    try {
        thing->testThrow(true);
        printf("C++ failed to catch C# exception in thing->testThrow??\n");
    }
    catch (InterfaceMethodException e) {
        printf("C++ side caught exception in C# thing: value %d\n", e.value);
    }
}

void startTimer(std::shared_ptr<IThing01> thing, int frequency) {
    printf("generated C++ fake startTimer()\n");
}

void stopTimer() {
    printf("generated C++ fake stopTimer()\n");
}
