#pragma once

#include "CoreStuff.h"

// our "exports" back to core (well, the DLL being built around core)
extern "C" {
    int nativeLibraryInit();
    void nativeLibraryShutdown();
    void ni_dumpTables();
    void ni_execServerFunc(int id);
    ni_ExceptionRef ni_execServerFuncWithExceptions(int id);
    void ni_releaseServerResource(int id);
    void ni_clearServerSafetyArea();
}
