#pragma once
#include "First.h"

namespace First
{
    namespace WhateverThing
    {

        void Handle__push(Handle value);
        Handle Handle__pop();

        void Handle_someMethod__wrapper();

        void Handle_dispose__wrapper();

        void create__wrapper();
    }

    int __register();
}
