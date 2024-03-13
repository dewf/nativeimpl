#include "generated/First.h"

namespace First
{
    namespace InterfaceGroup
    {
        // userType: MyInterface
    }
    void operateOnInterface(std::shared_ptr<InterfaceGroup::MyInterface> thing) {
        printf("C++ operateOnInterface, calling method:\n");
        thing->doesSomething("!!woooot!!");
    }
}
