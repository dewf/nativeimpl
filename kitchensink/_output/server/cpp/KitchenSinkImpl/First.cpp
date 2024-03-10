#include "generated/First.h"

namespace First
{
    namespace WhateverThing {
        struct __OpaquePrivate {
            // your content here
            int x;
        };
        void _method_someMethod(Opaque _this, int32_t x) {
        }
        void _method_dispose(Opaque _this) {
        }
    }
    WhateverThing::Opaque createThing() {
        return new WhateverThing::__OpaquePrivate;
    }
}
