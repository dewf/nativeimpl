#include "generated/First.h"

namespace First
{
    void operateOnThing(std::shared_ptr<WhateverThing> thing)
    {
        printf("backend calling thing->someMethod(...)\n");
        auto result = thing->someMethod("hello there");
        printf("backend result was: %d\n", result);
    }

    std::string doSomething(int32_t a, int32_t b) {
        char buffer[1024];
        sprintf_s<1024>(buffer, "a was %d, b was %d", a, b);
        return buffer;
    }

    void throwException(std::string content)
    {
        auto reason = "C++: calling 'content' was: " + content;
        throw CoolException(reason);
    }
}

