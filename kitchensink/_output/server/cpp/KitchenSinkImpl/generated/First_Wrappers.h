#pragma once
#include "First.h"

namespace First
{

    void __Int32_Option__push(std::optional<int32_t> value, bool isReturn);
    std::optional<int32_t> __Int32_Option__pop();

    void neatoMethod__wrapper();

    void returnsArray__wrapper();

    int __register();
}
