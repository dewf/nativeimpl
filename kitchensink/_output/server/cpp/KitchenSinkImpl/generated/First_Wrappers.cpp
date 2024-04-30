#include "../support/NativeImplServer.h"
#include "First_wrappers.h"
#include "First.h"

namespace First
{
    void __Thing2_Array__push(std::vector<std::shared_ptr<Thing2>> values, bool isReturn) {
        for (auto v = values.rbegin(); v != values.rend(); v++) {
            Thing2__push(*v, isReturn);
        }
        ni_pushSizeT(values.size());
    }

    std::vector<std::shared_ptr<Thing2>> __Thing2_Array__pop() {
        std::vector<std::shared_ptr<Thing2>> __ret;
        auto count = ni_popSizeT();
        for (auto i = 0; i < count; i++) {
            auto value = Thing2__pop();
            __ret.push_back(value);
        }
        return __ret;
    }

    class Thing1_PushVisitor : public Thing1Visitor {
    private:
        bool isReturn;
    public:
        Thing1_PushVisitor(bool isReturn) : isReturn(isReturn) {}
        void onValue1(const Value1* value1) override {
            // type:
            ni_pushInt32(0);
        }
        void onValue2(const Value2* value2) override {
            ni_pushInt32(value2->x);
            // type:
            ni_pushInt32(1);
        }
        void onValue3(const Value3* value3) override {
            pushStringInternal(value3->y);
            // type:
            ni_pushInt32(2);
        }
    };

    void Thing1__push(std::shared_ptr<Thing1> value, bool isReturn) {
        Thing1_PushVisitor v(isReturn);
        value->accept((Thing1Visitor*)&v);
    }

    std::shared_ptr<Thing1> Thing1__pop() {
        Thing1* thing = nullptr;
        switch (ni_popInt32()) {
        case 0: {
            thing = new Value1();
            break;
        }
        case 1: {
            auto x = ni_popInt32();
            thing = new Value2(x);
            break;
        }
        case 2: {
            auto s = popStringInternal();
            thing = new Value3(s);
            break;
        }
        default:
            printf("c++ Thing1__pop() - unknown kind! returning null\n");
        }
        return std::shared_ptr<Thing1>(thing);
    }

    class Thing2_PushVisitor : public Thing2Visitor {
    private:
        bool isReturn;
    public:
        Thing2_PushVisitor(bool isReturn) : isReturn(isReturn) {}
        void onVariant1(const Variant1* variant1) override {
            // kind
            ni_pushInt32(0);
        }
        void onVariant2(const Variant2* variant2) override {
            Thing1__push(variant2->thing, isReturn);
            // kind
            ni_pushInt32(1);
        }
        void onVariant3(const Variant3* variant3) override {
            __Thing2_Array__push(variant3->children, isReturn);
            // kind
            ni_pushInt32(2);
        }
    };

    void Thing2__push(std::shared_ptr<Thing2> value, bool isReturn) {
        Thing2_PushVisitor v(isReturn);
        value->accept((Thing2Visitor*)&v);
    }

    std::shared_ptr<Thing2> Thing2__pop() {
        Thing2* ret = nullptr;
        switch (ni_popInt32()) {
        case 0: {
            ret = new Variant1();
            break;
        }
        case 1: {
            auto thing = Thing1__pop();
            ret = new Variant2(thing);
            break;
        }
        case 2: {
            auto children = __Thing2_Array__pop();
            ret = new Variant3(children);
            break;
        }
        default:
            printf("c++ Thing2__pop() - unknown kind! returning null\n");
        }
        return std::shared_ptr<Thing2>(ret);
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
