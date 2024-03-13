#pragma once

#include "../support/NativeImplServer.h"
#include <functional>
#include <memory>
#include <string>
#include <vector>
#include <map>
#include <tuple>
#include <set>

namespace First
{
    namespace WhateverThing
    {

        struct __Handle; typedef struct __Handle* Handle;

        void Handle_someMethod(Handle _this, int32_t x);
        void Handle_dispose(Handle _this);
        Handle create();
    }
}
