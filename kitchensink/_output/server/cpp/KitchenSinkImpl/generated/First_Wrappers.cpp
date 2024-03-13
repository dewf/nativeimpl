#include "../support/NativeImplServer.h"
#include "First_wrappers.h"
#include "First.h"

namespace First
{
    namespace WhateverThing
    {
        void Handle__push(Handle value) {
            ni_pushPtr(value);
        }

        Handle Handle__pop() {
            return (Handle)ni_popPtr();
        }

        void Handle_someMethod__wrapper() {
            auto _this = Handle__pop();
            auto x = ni_popInt32();
            Handle_someMethod(_this, x);
        }

        void Handle_dispose__wrapper() {
            auto _this = Handle__pop();
            Handle_dispose(_this);
        }

        void create__wrapper() {
            Handle__push(create());
        }
    }

    int __register() {
        auto m = ni_registerModule("First");
        ni_registerModuleMethod(m, "WhateverThing.create", &WhateverThing::create__wrapper);
        ni_registerModuleMethod(m, "WhateverThing.Handle_someMethod", &WhateverThing::Handle_someMethod__wrapper);
        ni_registerModuleMethod(m, "WhateverThing.Handle_dispose", &WhateverThing::Handle_dispose__wrapper);
        return 0; // = OK
    }
}
