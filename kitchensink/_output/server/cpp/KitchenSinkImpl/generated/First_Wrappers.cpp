#include "../support/NativeImplServer.h"
#include "First_wrappers.h"
#include "First.h"

namespace First
{
    result::Result<int, std::string> __Result_int_string__pop() {
        auto isSuccess = ni_popBool();
        if (isSuccess) {
            return result::Ok(ni_popInt32());
        }
        return result::Err(popStringInternal());
    }

    void __Result_int_string__push(result::Result<int, std::string> value) {
        if (value.is_ok()) {
            ni_pushInt32(value.unwrap());
            ni_pushBool(true);
        }
        else {
            pushStringInternal(value.unwrap_err());
            ni_pushBool(false);
        }
    }

    std::optional<std::string> __Option_string_pop() {
        auto hasValue = ni_popBool();
        std::optional<std::string> maybeValue;
        if (hasValue) {
            maybeValue = popStringInternal();
        }
        return maybeValue;
    }

    void __Option_string_push(std::optional<std::string> value) {
        if (value.has_value()) {
            pushStringInternal(value.value());
        }
        ni_pushBool(value.has_value());
    }

    void doSomething__wrapper() {
        auto maybeString = __Option_string_pop();
        auto result = __Result_int_string__pop();
        doSomething(maybeString, result);
    }

    int __register() {
        auto m = ni_registerModule("First");
        ni_registerModuleMethod(m, "doSomething", &doSomething__wrapper);
        return 0; // = OK
    }
}
