#include "../support/NativeImplServer.h"
#include "First_wrappers.h"
#include "First.h"

namespace First
{
    void __Thing2_Array__push(std::vector<std::shared_ptr<Thing2::Base>> values, bool isReturn) {
        for (auto v = values.rbegin(); v != values.rend(); v++) {
            Thing2__push(*v, isReturn);
        }
        ni_pushSizeT(values.size());
    }

    std::vector<std::shared_ptr<Thing2::Base>> __Thing2_Array__pop() {
        std::vector<std::shared_ptr<Thing2::Base>> __ret;
        auto count = ni_popSizeT();
        for (auto i = 0; i < count; i++) {
            auto value = Thing2__pop();
            __ret.push_back(value);
        }
        return __ret;
    }

    class Thing1_PushVisitor : public Thing1::Visitor {
    private:
        bool isReturn;
    public:
        Thing1_PushVisitor(bool isReturn) : isReturn(isReturn) {}
        void onValue1(const Thing1::Value1* value1) override {
            // kind:
            ni_pushInt32(0);
        }
        void onValue2(const Thing1::Value2* value2) override {
            ni_pushInt32(value2->x);
            // kind:
            ni_pushInt32(1);
        }
        void onValue3(const Thing1::Value3* value3) override {
            pushStringInternal(value3->y);
            // kind:
            ni_pushInt32(2);
        }
        void onValue4(const Thing1::Value4* value4) override {
            Thing2__push(value4->other, isReturn);
            // kind:
            ni_pushInt32(3);
        }
    };

    void Thing1__push(std::shared_ptr<Thing1::Base> value, bool isReturn) {
        Thing1_PushVisitor v(isReturn);
        value->accept((Thing1::Visitor*)&v);
    }

    std::shared_ptr<Thing1::Base> Thing1__pop() {
        Thing1::Base* __ret = nullptr;
        switch (ni_popInt32()) {
        case 0: {
            __ret = new Thing1::Value1();
            break;
        }
        case 1: {
            auto x = ni_popInt32();
            __ret = new Thing1::Value2(x);
            break;
        }
        case 2: {
            auto y = popStringInternal();
            __ret = new Thing1::Value3(y);
            break;
        }
        case 3: {
            auto other = Thing2__pop();
            __ret = new Thing1::Value4(other);
            break;
        }
        default:
            printf("C++ Thing1__pop() - unknown kind! returning null\n");
        }
        return std::shared_ptr<Thing1::Base>(__ret);
    }

    class Thing2_PushVisitor : public Thing2::Visitor {
    private:
        bool isReturn;
    public:
        Thing2_PushVisitor(bool isReturn) : isReturn(isReturn) {}
        void onVariant1(const Thing2::Variant1* variant1) override {
            // kind:
            ni_pushInt32(0);
        }
        void onVariant2(const Thing2::Variant2* variant2) override {
            Thing1__push(variant2->thing, isReturn);
            // kind:
            ni_pushInt32(1);
        }
        void onVariant3(const Thing2::Variant3* variant3) override {
            __Thing2_Array__push(variant3->children, isReturn);
            // kind:
            ni_pushInt32(2);
        }
    };

    void Thing2__push(std::shared_ptr<Thing2::Base> value, bool isReturn) {
        Thing2_PushVisitor v(isReturn);
        value->accept((Thing2::Visitor*)&v);
    }

    std::shared_ptr<Thing2::Base> Thing2__pop() {
        Thing2::Base* __ret = nullptr;
        switch (ni_popInt32()) {
        case 0: {
            __ret = new Thing2::Variant1();
            break;
        }
        case 1: {
            auto thing = Thing1__pop();
            __ret = new Thing2::Variant2(thing);
            break;
        }
        case 2: {
            auto children = __Thing2_Array__pop();
            __ret = new Thing2::Variant3(children);
            break;
        }
        default:
            printf("C++ Thing2__pop() - unknown kind! returning null\n");
        }
        return std::shared_ptr<Thing2::Base>(__ret);
    }

    void doSomething__wrapper() {
        auto root = Thing2__pop();
        doSomething(root);
    }

    int __register() {
        auto m = ni_registerModule("First");
        ni_registerModuleMethod(m, "doSomething", &doSomething__wrapper);
        return 0; // = OK
    }
}
