#include "generated/First.h"

namespace First
{
    std::string doSomething(int32_t a, int32_t b) {
        char buffer[1024];
        sprintf_s<1024>(buffer, "a was %d, b was %d", a, b);
        return buffer;
    }
}

