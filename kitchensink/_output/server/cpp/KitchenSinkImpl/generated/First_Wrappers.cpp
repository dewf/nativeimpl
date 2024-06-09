#include "../support/NativeImplServer.h"
#include "First_wrappers.h"
#include "First.h"

namespace First
{
    namespace RootGroup {
        void CoolOpaque1__push(CoolOpaque1Ref value) {
            ni_pushPtr(value);
        }

        CoolOpaque1Ref CoolOpaque1__pop() {
            return (CoolOpaque1Ref)ni_popPtr();
        }
        namespace CoolOpaque1 {
            void MyEnum__push(MyEnum value) {
                ni_pushInt32((int32_t)value);
            }

            MyEnum MyEnum__pop() {
                auto tag = ni_popInt32();
                return (MyEnum)tag;
            }

            void create__wrapper() {
                CoolOpaque1__push(create());
            }
        }

        void CoolOpaque1_dispose__wrapper() {
            auto _this = CoolOpaque1__pop();
            CoolOpaque1_dispose(_this);
        }
    }
    void RootOpaque__push(RootOpaqueRef value) {
        ni_pushPtr(value);
    }

    RootOpaqueRef RootOpaque__pop() {
        return (RootOpaqueRef)ni_popPtr();
    }
    namespace RootOpaque {

        void makeOne__wrapper() {
            RootOpaque__push(makeOne());
        }
    }

    void RootOpaque_dispose__wrapper() {
        auto _this = RootOpaque__pop();
        RootOpaque_dispose(_this);
    }

    int __register() {
        auto m = ni_registerModule("First");
        ni_registerModuleMethod(m, "RootOpaque_dispose", &RootOpaque_dispose__wrapper);
        ni_registerModuleMethod(m, "RootGroup.CoolOpaque1_dispose", &RootGroup::CoolOpaque1_dispose__wrapper);
        ni_registerModuleMethod(m, "RootGroup.CoolOpaque1.create", &RootGroup::CoolOpaque1::create__wrapper);
        ni_registerModuleMethod(m, "RootOpaque.makeOne", &RootOpaque::makeOne__wrapper);
        return 0; // = OK
    }
}
