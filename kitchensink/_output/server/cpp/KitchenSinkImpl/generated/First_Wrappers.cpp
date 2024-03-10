#include "../support/NativeImplServer.h"
#include "First_wrappers.h"
#include "First.h"

namespace First
{
    namespace WhateverThing {
        void __push(Opaque value) {
            ni_pushPtr(value);
        }
        Opaque __pop() {
            return (Opaque)ni_popPtr();
        }
        void _method_someMethod__wrapper() {
            auto _this = __pop();
            auto x = ni_popInt32();
            _method_someMethod(_this, x);
        }
        void _method_dispose__wrapper() {
            auto _this = __pop();
            _method_dispose(_this);
        }
    }
    void createThing__wrapper() {
        WhateverThing::__push(createThing());
    }

    int __register() {
        auto m = ni_registerModule("First");
        ni_registerModuleMethod(m, "createThing", &createThing__wrapper);
        ni_registerModuleMethod(m, "WhateverThing_someMethod", &WhateverThing::_method_someMethod__wrapper);
        ni_registerModuleMethod(m, "WhateverThing_dispose", &WhateverThing::_method_dispose__wrapper);
        return 0; // = OK
    }
}
