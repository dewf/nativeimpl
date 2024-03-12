#include "../support/NativeImplServer.h"
#include "First_wrappers.h"
#include "First.h"

namespace First
{
    namespace Nested
    {
        namespace WhateverThing
        {
            void __push(Opaque value) {
                ni_pushPtr(value);
            }

            Opaque __pop() {
                return (Opaque)ni_popPtr();
            }

            void someMethod__wrapper() {
                auto _this = __pop();
                auto x = ni_popInt32();
                someMethod(_this, x);
            }

            void dispose__wrapper() {
                auto _this = __pop();
                dispose(_this);
            }
        }
    }

    void createThing__wrapper() {
        Nested::WhateverThing::__push(createThing());
    }

    int __register() {
        auto m = ni_registerModule("First");
        ni_registerModuleMethod(m, "createThing", &createThing__wrapper);
        ni_registerModuleMethod(m, "Nested.WhateverThing.someMethod", &Nested::WhateverThing::someMethod__wrapper);
        ni_registerModuleMethod(m, "Nested.WhateverThing.dispose", &Nested::WhateverThing::dispose__wrapper);
        return 0; // = OK
    }
}
