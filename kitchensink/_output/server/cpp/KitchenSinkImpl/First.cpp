#include "generated/First.h"

namespace First
{
    class MyThing : public ServerCoolThing {
        void doSomething(int x) override {
            throw WootProblem("yazoooo");
        }
    };
    std::shared_ptr<CoolThing> createThing() {
        return std::shared_ptr<CoolThing>(new MyThing());
    }
}
