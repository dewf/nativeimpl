#pragma once
#include "First.h"

namespace First
{
    void Thing1__push(std::shared_ptr<Thing1::Base> value, bool isReturn);
    std::shared_ptr<Thing1::Base> Thing1__pop();
    void Thing2__push(std::shared_ptr<Thing2::Base> value, bool isReturn);
    std::shared_ptr<Thing2::Base> Thing2__pop();

    void doSomething__wrapper();

    int __register();
}
