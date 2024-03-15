#include "generated/First.h"

namespace First
{
    // userType: ITestInterface
    void operateOnInterface(std::shared_ptr<ITestInterface> thing) {
        printf("C++ calling interface method ->wootMethod(1234)\n");
        thing->wootMethod(1234);
    }

    class MyInterface : public ITestInterface {
    public:
        void wootMethod(int32_t x) override {
            printf("C++ server MyInterface: .wootMethod(%d)\n", x);
        }
    };

    std::shared_ptr<ITestInterface> serverCreate()
    {
        return std::shared_ptr<ITestInterface>(new MyInterface());
    }
}
