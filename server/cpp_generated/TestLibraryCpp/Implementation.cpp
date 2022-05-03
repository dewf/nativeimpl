#include "Implementation.h"

void takesThing(std::shared_ptr<MyThing> thing) {
    printf("C++ takesThing:\n");
    printf("  - thing->doSomething(1234) = %d\n", thing->doSomething(1234));
}

class FakeThing : public ServerMyThing {
public:
    int doSomething(int x) override {
        auto res = x + 2002;
        printf("C++ FakeThing.doSomething(%d) = %d\n", x, res);
        return res;
    }
};

std::shared_ptr<MyThing> returnsThing() {
    return std::shared_ptr<MyThing>(new FakeThing());
}

void applyFunc(std::function<IntFunc> f) {
    printf("C++ applyFunc(1234) = %d\n", f(1234));
}
