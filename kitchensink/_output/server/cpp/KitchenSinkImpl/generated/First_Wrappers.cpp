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

    void __int32_string_Result_Array__push(std::vector<result::Result<int32_t, std::string>> values, bool isReturn) {
        std::vector<bool> isSuccessArray;
        isSuccessArray.reserve(values.size());
        std::vector<int32_t> successValuesArray;
        std::vector<std::string> errorValuesArray;
        for (auto i = values.begin(); i != values.end(); i++) {
            if (i->is_ok()) {
                successValuesArray.push_back(i->unwrap());
                isSuccessArray.push_back(true);
            }
            else {
                errorValuesArray.push_back(i->unwrap_err());
                isSuccessArray.push_back(false);
            }
        }
        pushStringArrayInternal(errorValuesArray);
        pushInt32ArrayInternal(successValuesArray);
        pushBoolArrayInternal(isSuccessArray);
    }

    std::vector<result::Result<int32_t, std::string>> __int32_string_Result_Array__pop() {
        auto isSuccessArray = popBoolArrayInternal();
        auto successValuesArray = popInt32ArrayInternal();
        auto errorValuesArray = popStringArrayInternal();
        std::vector<result::Result<int32_t, std::string>> ret;
        ret.reserve(isSuccessArray.size());
        auto succPtr = successValuesArray.data();
        auto errPtr = errorValuesArray.data();
        for (auto i = isSuccessArray.begin(); i != isSuccessArray.end(); i++) {
            if (*i) {
                ret.push_back(result::Ok(*succPtr++));
            }
            else {
                ret.push_back(result::Err(*errPtr++));
            }
        }
        return ret;
    }

    void method1__wrapper() {
        auto resultArray = __int32_string_Result_Array__pop();
        method1(resultArray);
    }

    void method2__wrapper() {
        __int32_string_Result_Array__push(method2(), true);
    }

    int __register() {
        auto m = ni_registerModule("First");
        ni_registerModuleMethod(m, "method1", &method1__wrapper);
        ni_registerModuleMethod(m, "method2", &method2__wrapper);
        return 0; // = OK
    }
}
