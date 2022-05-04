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

void takesBuffer(std::shared_ptr<NativeBuffer<int>> buffer)
{
    printf("C++ takesBuffer:\n");
    size_t length;
    auto ptr = buffer->getSpan(&length);
    for (auto i = 0; i < length; i++) {
        printf(" -> index %d: value %d\n", i, ptr[i]);
        // add 100 to value
        ptr[i] += 100;
    }
    printf("C++ takesBuffer done!\n");
}
