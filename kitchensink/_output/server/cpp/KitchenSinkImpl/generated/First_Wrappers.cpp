#include "../support/NativeImplServer.h"
#include "First_wrappers.h"
#include "First.h"

namespace First
{

    void __Int32_String_Result__push(result::Result<int32_t, std::string> value, bool isReturn) {
        if (value.is_ok()) {
            ni_pushInt32(value.unwrap());
            ni_pushBool(true);
        }
        else {
            pushStringInternal(value.unwrap_err());
            ni_pushBool(false);
        }
    }

    result::Result<int32_t, std::string> __Int32_String_Result__pop() {
        auto isSuccess = ni_popBool();
        if (isSuccess) {
            auto successValue = ni_popInt32();
            return result::Ok(successValue);
        }
        auto errValue = popStringInternal();
        return result::Err(errValue);
    }

    void __String_Option__push(std::optional<std::string> value, bool isReturn) {
        if (value.has_value()) {
            pushStringInternal(value.value());
            ni_pushBool(true);
        }
        else {
            ni_pushBool(false);
        }
    }

    std::optional<std::string> __String_Option__pop() {
        std::optional<std::string> maybeValue;
        auto hasValue = ni_popBool();
        if (hasValue) {
            maybeValue =  popStringInternal();
        }
        return maybeValue;
    }

    void doSomething__wrapper() {
        auto maybeString = __String_Option__pop();
        auto result = __Int32_String_Result__pop();
        doSomething(maybeString, result);
    }

    int __register() {
        auto m = ni_registerModule("First");
        ni_registerModuleMethod(m, "doSomething", &doSomething__wrapper);
        return 0; // = OK
    }
}
