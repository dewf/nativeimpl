#include "Implementation.h"
#include <memory>
#include <functional>

int arrayTest(std::vector<int> values) {
    printf("hello from C++ array test!\n");
    for (auto i = values.begin(); i != values.end(); i++) {
        printf(" - %d\n", *i);
    }
    return 12345;
}

std::vector<int> returnArrayTest() {
    return std::vector<int>({ 50, 60, 70, 80, 90, 100, 98765 });
}
