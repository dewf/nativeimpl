#include <cstdio>
#include "NativeImplServer.h"
#include "Implementation.h"

#include <memory>
#include <functional>

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

// global module constants
const int global01 = 101;
const int anotherGlobal = 202;
const int lastIntGlobal = 3003;

void stringTest(std::string str)
{
    printf("C++ stringTest: got [%s]\n", str.c_str());
}

std::string returnString()
{
    return "here's a string from C++!";
}

int sum(int a, int b, int c) {
    return a + b + c;
}

int execFunc01(std::function<int(int)> singleIntFunc) {
    return singleIntFunc(101);
}

int defaultStashedFunc(int x) {
    printf("defaultStashedFunc! x * 1001\n");
    fflush(stdout);
    return x * 1001;
}

static std::function<int(int)> stashedFunc = defaultStashedFunc;

void stashFunc(std::function<int(int)> singleIntFunc) {
    printf("library: stashFunc(sif)\n");
    stashedFunc = singleIntFunc;
}

void releaseFunc() {
    printf("releasing stashed func...\n");
    stashedFunc = defaultStashedFunc;
}

std::function<int(int)> returnStashedFunc() {
    printf("returning stashed func...\n");
    return stashedFunc;
}

class RealThing01 : public ServerThing01 {
private:
    std::shared_ptr<IThing01> stashedThing;
public:
    int someMethod(int x) override {
        return x * 321;
    }

    void stashThing(std::shared_ptr<IThing01> subThing) override {
        stashedThing = subThing;
    }

    std::shared_ptr<IThing01> getStashedThing() override {
        auto ret = stashedThing;
        stashedThing.reset();
        return ret;
    }

    void testThrow(bool doThrow) override {
        if (doThrow) {
            throw InterfaceMethodException(1234);
        }
    }

    void onTimerTick() override {
        printf("RealThing01.onTimerTick() !!\n");
    }

    ~RealThing01() override {
        //printf("real thing 01 dtor!!!\n");
    }
};

std::shared_ptr<IThing01> makeThing() {
    return std::shared_ptr<IThing01>(new RealThing01());
}

int operateOnThing(std::shared_ptr<IThing01> thing, int arg) {
    printf("C++ operateOnThing:\n");
    auto res = thing->someMethod(arg);
    printf(" - thing->someMethod(%d) = %d\n", arg, res);
    return res;
}

static std::shared_ptr<IThing01> savedThing;

void saveThing(std::shared_ptr<IThing01> thing) {
    printf("C++ saving thing:\n");
    savedThing = thing;
}

std::shared_ptr<IThing01> getSavedThing() {
    printf("C++ returning thing:\n");
    auto toReturn = savedThing;
    savedThing.reset(); // won't this destroy the remote client object as soon as this function exits? (when toReturn leaves scope)
    return toReturn;
}

void clientStashTest(std::shared_ptr<IThing01> thing) {
    // make a server-side thing
    auto arg = std::shared_ptr<IThing01>(new RealThing01());

    printf("C++ clientStashTest: stashing thing...\n");
    thing->stashThing(arg);

    printf("C++ clientStashTest: getStashedThing...\n");
    auto ret = thing->getStashedThing();
}

void testNegative(int value) {
    if (value < 0) {
        throw NegativeException(value);
    }
}

void testClientThrow(std::shared_ptr<IThing01> thing) {
    printf("C++ testClientThrow:\n");
    printf("  - testThrow(false)\n");
    thing->testThrow(false);
    printf("  - testThrow(true)\n");
    try {
        thing->testThrow(true);
        printf(" ... uhh C++ did not catch expected client exception ...\n");
    }
    catch (const InterfaceMethodException& e) {
        printf("*** C++ caught InterfaceMethodException(%d)!! ***\n", e.value);
    }
}

static HANDLE timerHandle;
static std::shared_ptr<IThing01> timerThing;
static bool timerAlive = false;

static VOID CALLBACK timerCallback(PVOID lpParameter, BOOLEAN timerOrWaitFired) {
    printf("C++ timer callback!\n");
    if (timerAlive) {
        timerThing->onTimerTick();
    }
    else {
        printf("C++ timer: (ignoring tick with timerAlive=false)\n");
    }
}

void startTimer(std::shared_ptr<IThing01> thing, int freq)
{
    timerThing = thing;

    timerAlive = true;
    if (!CreateTimerQueueTimer(
        &timerHandle,
        NULL, // default timer queue,
        &timerCallback,
        NULL, // no param
        0, // start delay
        1000 / freq,
        0 // no flags
    )) {
        printf("C++ startTimer: timer creation failed\n");
    }
}

void stopTimer() {
    timerAlive = false;

    DeleteTimerQueueTimer(
        NULL, // default queue
        timerHandle,
        INVALID_HANDLE_VALUE // wait for completion before returning. is this 100%?
    );

    timerThing.reset(); // don't need this anymore ...

    printf("C++ stopTimer() done\n");
}