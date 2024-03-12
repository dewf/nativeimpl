#include "generated/First.h"

namespace First
{
    namespace Nested
    {
        namespace WhateverThing
        {
            struct __OpaquePrivate {
                // your content here
                // (or simply cast your existing pointer to 'Opaque')
                int x;
            };
            void someMethod(Opaque _this, int32_t x) {
                _this->x += x;
            }
            void dispose(Opaque _this) {
                printf("C++ disposing WhateverThing, inner value: %d\n", _this->x);
            }
        }
    }
    Nested::WhateverThing::Opaque createThing() {
        return new Nested::WhateverThing::__OpaquePrivate{ 0 };
    }
}
