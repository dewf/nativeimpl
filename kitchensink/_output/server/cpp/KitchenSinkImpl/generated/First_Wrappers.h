#pragma once
#include "First.h"

namespace First
{

    void __Int32_String_Result__push(result::Result<int32_t, std::string> value, bool isReturn);
    result::Result<int32_t, std::string> __Int32_String_Result__pop();

    void __String_Option__push(std::optional<std::string> value, bool isReturn);
    std::optional<std::string> __String_Option__pop();

    void doSomething__wrapper();

    int __register();
}
