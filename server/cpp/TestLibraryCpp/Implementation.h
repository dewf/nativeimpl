#pragma once

#include <functional>
#include <memory>

class IThing01 {
public:
    virtual int someMethod(int x) = 0;
    virtual void stashThing(std::shared_ptr<IThing01> subThing) = 0;
    virtual std::shared_ptr<IThing01> getStashedThing() = 0;
    virtual void testThrow(bool doThrow) = 0; // throws InterfaceMethodException
    virtual void onTimerTick() = 0;
};

extern const int global01;
extern const int anotherGlobal;
extern const int lastIntGlobal;

int sum(int a, int b, int c);
int execFunc01(std::function<int(int)> singleIntFunc);
int defaultStashedFunc(int x);
void stashFunc(std::function<int(int)> singleIntFunc);
void releaseFunc();
std::function<int(int)> returnStashedFunc();
std::shared_ptr<IThing01> makeThing();
int operateOnThing(std::shared_ptr<IThing01> thing, int arg);
void saveThing(std::shared_ptr<IThing01> thing);
std::shared_ptr<IThing01> getSavedThing();
void clientStashTest(std::shared_ptr<IThing01> thing);
void testNegative(int value); // throws NegativeException
void testClientThrow(std::shared_ptr<IThing01> thing);
void startTimer(std::shared_ptr<IThing01> thing, int freq);
void stopTimer();
