#pragma once
#include "First.h"

namespace First
{
    namespace InterfaceGroup
    {

        void MyInterface__push(std::shared_ptr<MyInterface> inst, bool isReturn);
        std::shared_ptr<MyInterface> MyInterface__pop();

        void MyInterface_doesSomething__wrapper(int serverID);
    }

    void operateOnInterface__wrapper();

    int __register();
}
