#include "NativeImplServer.h"
#include "Implementation.h"


void arrayTest__wrapper() {
    auto values = popIntArrayInternal();
    ni_pushInt(arrayTest(values));
}

void returnArrayTest__wrapper() {
    pushIntArrayInternal(returnArrayTest());
}

extern "C" int nativeLibraryInit() {
    auto m = ni_registerModule("TestLibrary");
    ni_registerModuleMethod(m, "arrayTest", &arrayTest__wrapper);
    ni_registerModuleMethod(m, "returnArrayTest", &returnArrayTest__wrapper);
    return 0; // = OK
}

extern "C" void nativeLibraryShutdown() {
}
