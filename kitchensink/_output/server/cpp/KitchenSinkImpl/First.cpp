#include "generated/First.h"

namespace First
{
    void doSomething(std::optional<std::string> maybeString, result::Result<int, std::string> result)
    {
        printf("C++ doSomething:\n");
        printf("====== maybeString ========\n");
        if (maybeString.has_value()) {
            printf(" - hasValue: %s\n", maybeString.value().c_str());
        }
        else {
            printf(" - (no value)\n");
        }

        printf("====== result =============\n");
        if (result.is_ok()) {
            printf(" - result OK: %d\n", result.unwrap());
        }
        else {
            printf(" - result Error: %s\n", result.unwrap_err().c_str());
        }
    }
}
