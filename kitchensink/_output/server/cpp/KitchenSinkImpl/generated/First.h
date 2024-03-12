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
    namespace Nested
    {

        namespace WhateverThing
        {
            struct __OpaquePrivate; typedef struct __OpaquePrivate* Opaque;
            void someMethod(Opaque _this, int32_t x);
            void dispose(Opaque _this);
        }
    }
    Nested::WhateverThing::Opaque createThing();
}
