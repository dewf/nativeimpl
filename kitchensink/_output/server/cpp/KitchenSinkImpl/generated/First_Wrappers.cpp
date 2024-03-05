#include "../support/NativeImplServer.h"
#include "First_wrappers.h"
#include "First.h"

namespace First
{

    void doSomething__wrapper() {
        auto a = ni_popInt32();
        auto b = ni_popInt32();
        pushStringInternal(doSomething(a, b));
    }

    int __register() {
        auto m = ni_registerModule("First");
        ni_registerModuleMethod(m, "doSomething", &doSomething__wrapper);
        return 0; // = OK
    }
}
