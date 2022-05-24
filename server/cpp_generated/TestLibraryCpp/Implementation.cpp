#include "Implementation.h"

class MyThing : public ServerCoolThing {
public:
    int32_t method1(std::string x) override {
        printf("C++ MyThing->method1(%s)\n", x.c_str());
        return 1001;
    }
};

int32_t doSomething(int32_t x, std::string y) {
    printf("C++ doSomething(%d, %s)\n", x, y.c_str());
    return 1234;
}
