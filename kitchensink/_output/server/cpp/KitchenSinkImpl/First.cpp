#include "generated/First.h"

namespace First
{
    class Visitor1 : public Thing1::Visitor {
    public:
        void onValue1(const Thing1::Value1* value1) override {
            printf(" - we got a value1\n");
        }
        void onValue2(const Thing1::Value2* value1) override {
            printf(" - we got a value2: %d\n", value1->x);
        }
        void onValue3(const Thing1::Value3* value1) override {
            printf(" - we got a value3: %s\n", value1->y.c_str());
        }
        void onValue4(const Thing1::Value4* value4) override {
            printf(" - we got a value4!\n");
        }
    };

    class MyVisitor : public Thing2::Visitor {
    public:
        void onVariant1(const Thing2::Variant1 *variant1) {
            printf("we got a variant 1!\n");
        }
        void onVariant2(const Thing2::Variant2 *variant2) {
            printf("we got a variant 2! submatch:\n");
            Visitor1 v1;
            variant2->thing->accept((Thing1::Visitor*)&v1);
        }
        void onVariant3(const Thing2::Variant3 *variant3) {
            printf("we got a variant 3 with %d children!\n", (int)variant3->children.size());
        }
    };

    void doSomething(std::shared_ptr<Thing2::Base> root)
    {
        MyVisitor v;
        root->accept((Thing2::Visitor*)&v);
    }
}
