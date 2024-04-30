#pragma once

#include "../support/NativeImplServer.h"
#include <functional>
#include <memory>
#include <string>
#include <vector>
#include <map>
#include <tuple>
#include <set>
#include <optional>
#include "../support/result.h"

namespace First
{
    // THING1 STUFF ============================================================
    class Value1;
    class Value2;
    class Value3;

    class Thing1Visitor {
    public:
        virtual void onValue1(const Value1 *value1) = 0;
        virtual void onValue2(const Value2 *value2) = 0;
        virtual void onValue3(const Value3 *value3) = 0;
    };
    
    class Thing1 { //: public std::enable_shared_from_this<Thing1> {
    public:
        virtual void accept(Thing1Visitor *visitor) = 0;
    };

    class Value1 : public Thing1 {
    public:
        Value1() {
        }
        void accept(Thing1Visitor* visitor) override {
            //auto x = std::static_pointer_cast<Value1>(shared_from_this());
            visitor->onValue1(this);
        }
    };

    class Value2 : public Thing1 {
    public:
        const int x;
        Value2(int x) : x(x) {}
        void accept(Thing1Visitor* visitor) override {
            //auto x = std::static_pointer_cast<Value2>(shared_from_this());
            visitor->onValue2(this);
        }
    };
    
    class Value3 : public Thing1 {
    public:
        std::string y;
        Value3(std::string y) : y(y) {}
        void accept(Thing1Visitor* visitor) override {
            //auto x = std::static_pointer_cast<Value3>(shared_from_this());
            visitor->onValue3(this);
        }
    };

    // THING2 STUFF ============================================================
    class Variant1;
    class Variant2;
    class Variant3;

    class Thing2Visitor {
    public:
        virtual void onVariant1(const Variant1 *variant1) = 0;
        virtual void onVariant2(const Variant2 *variant2) = 0;
        virtual void onVariant3(const Variant3 *variant3) = 0;
    };

    class Thing2 { // : public std::enable_shared_from_this<Thing2> {
    public:
        virtual void accept(Thing2Visitor *visitor) = 0;
    };

    class Variant1 : public Thing2 {
    public:
        Variant1() {}
        void accept(Thing2Visitor* visitor) override {
            //auto x = std::static_pointer_cast<Variant1>(shared_from_this());
            visitor->onVariant1(this);
        }
    };

    class Variant2 : public Thing2 {
    public:
        const std::shared_ptr<Thing1> thing;
        Variant2(std::shared_ptr<Thing1> thing) : thing(thing) {}
        void accept(Thing2Visitor* visitor) override {
            //auto x = std::static_pointer_cast<Variant2>(shared_from_this());
            visitor->onVariant2(this);
        }
    };

    class Variant3 : public Thing2 {
    public:
        const std::vector<std::shared_ptr<Thing2>> children;
        Variant3(std::vector<std::shared_ptr<Thing2>> children) : children(children) {}
        void accept(Thing2Visitor* visitor) override {
            //auto x = std::static_pointer_cast<Variant3>(shared_from_this());
            visitor->onVariant3(this);
        }
    };

    void doSomething(std::shared_ptr<Thing2> root);
}
