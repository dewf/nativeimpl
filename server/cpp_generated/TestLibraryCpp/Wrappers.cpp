#include "NativeImplServer.h"
#include "Implementation.h"


// built-in array type: std::vector<int32_t>

void sumValues__wrapper() {
    auto values = popInt32ArrayInternal();
    auto threadId = popStringInternal();
    ni_pushInt32(sumValues(values, threadId));
}

extern "C" int nativeLibraryInit() {
    auto m = ni_registerModule("TestLibrary");
    ni_registerModuleMethod(m, "sumValues", &sumValues__wrapper);
    return 0; // = OK
}

extern "C" void nativeLibraryShutdown() {
}
