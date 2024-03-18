#include "../support/NativeImplServer.h"
#include "First_wrappers.h"
#include "First.h"

namespace First
{

    void __Int32_Option__push(std::optional<int32_t> value, bool isReturn) {
        if (value.has_value()) {
            ni_pushInt32(value.value());
            ni_pushBool(true);
        }
        else {
            ni_pushBool(false);
        }
    }

    std::optional<int32_t> __Int32_Option__pop() {
        std::optional<int32_t> maybeValue;
        auto hasValue = ni_popBool();
        if (hasValue) {
            maybeValue =  ni_popInt32();
        }
        return maybeValue;
    }
    void __Int32_Option_Array__push(std::vector<std::optional<int32_t>> values, bool isReturn) {
        std::vector<bool> hasValueArray;
        hasValueArray.reserve(values.size());
        std::vector<int32_t> valueArray;
        for (auto i = values.begin(); i != values.end(); i++) {
            if (i->has_value()) {
                valueArray.push_back(i->value());
                hasValueArray.push_back(true);
            }
            else {
                hasValueArray.push_back(false);
            }
        }
        pushInt32ArrayInternal(valueArray);
        pushBoolArrayInternal(hasValueArray);
    }

    std::vector<std::optional<int32_t>> __Int32_Option_Array__pop() {
        auto hasValueArray = popBoolArrayInternal();
        auto valueArray = popInt32ArrayInternal();
        std::vector<std::optional<int32_t>> ret(hasValueArray.size());
        auto valuePtr = valueArray.data();
        for (auto i = 0; i < hasValueArray.size(); i++) {
            if (hasValueArray[i]) {
                ret[i] = *valuePtr++;
            }
        }
        return ret;
    }

    void neatoMethod__wrapper() {
        auto maybeArray = __Int32_Option_Array__pop();
        neatoMethod(maybeArray);
    }

    void returnsArray__wrapper() {
        __Int32_Option_Array__push(returnsArray(), true);
    }

    int __register() {
        auto m = ni_registerModule("First");
        ni_registerModuleMethod(m, "neatoMethod", &neatoMethod__wrapper);
        ni_registerModuleMethod(m, "returnsArray", &returnsArray__wrapper);
        return 0; // = OK
    }
}
