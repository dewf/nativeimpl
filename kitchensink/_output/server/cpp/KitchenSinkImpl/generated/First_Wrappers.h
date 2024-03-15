#pragma once
#include "First.h"

namespace First
{

    void ITestInterface__push(std::shared_ptr<ITestInterface> inst, bool isReturn);
    std::shared_ptr<ITestInterface> ITestInterface__pop();

    void ITestInterface_wootMethod__wrapper(int serverID);

    void operateOnInterface__wrapper();

    int __register();
}
