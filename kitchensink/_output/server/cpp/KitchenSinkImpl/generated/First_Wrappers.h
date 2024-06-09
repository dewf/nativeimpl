#pragma once
#include "First.h"

namespace First
{
    namespace RootGroup {

        void CoolOpaque1__push(CoolOpaque1Ref value);
        CoolOpaque1Ref CoolOpaque1__pop();
        namespace CoolOpaque1 {

            void MyEnum__push(MyEnum value);
            MyEnum MyEnum__pop();

            void create__wrapper();
        }

        void CoolOpaque1_dispose__wrapper();
    }

    void RootOpaque__push(RootOpaqueRef value);
    RootOpaqueRef RootOpaque__pop();
    namespace RootOpaque {

        void makeOne__wrapper();
    }

    void RootOpaque_dispose__wrapper();

    int __register();
}
