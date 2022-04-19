#include "Implementation.h"

int someFunction(CoolEnum woot) {
    switch (woot.tag) {
    case CoolEnum::Tag::Value1:
        // no args
        break;
    case CoolEnum::Tag::Value2:
        woot.value2->x;
        break;
    case CoolEnum::Tag::Value3:
        woot.value3->m;
        woot.value3->things;
        break;
    }
    auto v1 = CoolEnum::Value1::make();
    auto v2 = CoolEnum::Value2::make(101);
    auto v3 = CoolEnum::Value3::make("yowza", { 1, 2, 3, 4, 5 });
    return 0;
}
