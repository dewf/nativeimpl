#pragma once
#include "First.h"

namespace First
{
    namespace Nested
    {
        namespace WhateverThing
        {

            void __push(Opaque value);
            Opaque __pop();

            void someMethod__wrapper();

            void dispose__wrapper();
        }
    }

    void createThing__wrapper();

    int __register();
}
