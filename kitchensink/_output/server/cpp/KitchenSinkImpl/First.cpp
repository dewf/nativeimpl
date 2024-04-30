#include "generated/First.h"

namespace First
{
    class Visitor1 : public Thing1Visitor {
    public:
        void onValue1(const Value1* value1) {
            printf(" - we got a value1\n");
        }
        void onValue2(const Value2* value1) {
            printf(" - we got a value2: %d\n", value1->x);
        }
        void onValue3(const Value3* value1) {
            printf(" - we got a value3: %s\n", value1->y.c_str());
        }
    };

    class MyVisitor : public Thing2Visitor {
    public:
        void onVariant1(const Variant1 *variant1) {
            printf("we got a variant 1!\n");
        }
        void onVariant2(const Variant2 *variant2) {
            printf("we got a variant 2! submatch:\n");
            Visitor1 v1;
            variant2->thing->accept((Thing1Visitor*)&v1);
        }
        void onVariant3(const Variant3 *variant3) {
            printf("we got a variant 3 with %d children!\n", (int)variant3->children.size());
        }
    };

    void doSomething(std::shared_ptr<Thing2> root)
    {
        MyVisitor v;
        root->accept((Thing2Visitor*)&v);
    }
}
