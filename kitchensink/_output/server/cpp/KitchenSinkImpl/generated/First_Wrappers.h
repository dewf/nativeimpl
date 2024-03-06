#pragma once
#include "First.h"

namespace First
{

    void WhateverThing__push(std::shared_ptr<WhateverThing> inst, bool isReturn);
    std::shared_ptr<WhateverThing> WhateverThing__pop();

    void WhateverThing_someMethod__wrapper(int serverID);

    void operateOnThing__wrapper();

    void doSomething__wrapper();

    void CoolException__push(CoolException e);
    void CoolException__buildAndThrow();

    void throwException__wrapper();

    int __register();
}
