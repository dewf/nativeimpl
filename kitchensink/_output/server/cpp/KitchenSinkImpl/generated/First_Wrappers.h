#pragma once
#include "First.h"

namespace First
{
    namespace WhateverThing {
        void __push(Opaque value);
        Opaque __pop();
        void _method_someMethod__wrapper();
        void _method_dispose__wrapper();
    }
    void createThing__wrapper();

    int __register();
}
