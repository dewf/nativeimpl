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
    // opaque WhateverThing
    namespace WhateverThing {
        struct __OpaquePrivate; typedef struct __OpaquePrivate* Opaque;
        // any static definitions will appear here (eg enums, structs, etc)
        // order is important because of C++ rules :(
        // ...

        // methods
        void _method_someMethod(Opaque _this, int32_t x);
        void _method_dispose(Opaque _this);
    }

    WhateverThing::Opaque createThing();
}
