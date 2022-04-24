#include "NativeImplServer.h"
#include "Implementation.h"


void simpleFunction__wrapper() {
    auto x = ni_popInt();
    ni_pushInt(simpleFunction(x));
}

extern "C" int nativeLibraryInit() {
    auto m = ni_registerModule("TestLibrary");
    ni_registerModuleMethod(m, "simpleFunction", &simpleFunction__wrapper);
    return 0; // = OK
}

extern "C" void nativeLibraryShutdown() {
}
