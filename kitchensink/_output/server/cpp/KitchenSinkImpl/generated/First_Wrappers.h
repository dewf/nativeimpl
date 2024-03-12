#pragma once
#include "First.h"

namespace First
{

    void WootProblem__push(WootProblem e);
    void WootProblem__buildAndThrow();

    void CoolThing__push(std::shared_ptr<CoolThing> inst, bool isReturn);
    std::shared_ptr<CoolThing> CoolThing__pop();

    void CoolThing_doSomething__wrapper(int serverID);

    void createThing__wrapper();

    int __register();
}
