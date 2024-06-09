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

    struct __RootOpaque; typedef struct __RootOpaque* RootOpaqueRef;
    namespace RootGroup {

        struct __CoolOpaque1; typedef struct __CoolOpaque1* CoolOpaque1Ref;

        namespace CoolOpaque1 {

            enum class MyEnum {
                Value1,
                Value2
            };
            CoolOpaque1Ref create();
        }
        void CoolOpaque1_dispose(CoolOpaque1Ref _this);
    }

    namespace RootOpaque {
        RootOpaqueRef makeOne();
    }
    void RootOpaque_dispose(RootOpaqueRef _this);
}
