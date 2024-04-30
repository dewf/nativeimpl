#pragma once
#include "First.h"

namespace First
{
    void Thing1__push(std::shared_ptr<Thing1> value, bool isReturn);
    std::shared_ptr<Thing1> Thing1__pop();

    void Thing2__push(std::shared_ptr<Thing2> value, bool isReturn);
    std::shared_ptr<Thing2> Thing2__pop();

    void doSomething__wrapper();

    int __register();
}
