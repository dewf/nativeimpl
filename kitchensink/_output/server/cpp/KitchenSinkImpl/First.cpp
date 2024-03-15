#include "generated/First.h"

namespace First
{
    // userType: ITestInterface
    void operateOnInterface(std::shared_ptr<ITestInterface> thing) {
        printf("C++ calling interface method ->wootMethod(1234)\n");
        thing->wootMethod(1234);
    }
}
