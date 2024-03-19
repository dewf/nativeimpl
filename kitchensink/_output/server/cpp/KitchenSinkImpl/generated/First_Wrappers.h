#pragma once
#include "First.h"

namespace First
{

    void __Int32_String_Result__push(result::Result<int32_t, std::string> value, bool isReturn);
    result::Result<int32_t, std::string> __Int32_String_Result__pop();

    void method1__wrapper();

    void method2__wrapper();

    int __register();
}
