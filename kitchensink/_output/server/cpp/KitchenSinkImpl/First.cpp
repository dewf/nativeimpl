#include "generated/First.h"

namespace First
{
    void neatoMethod(std::vector<std::optional<int32_t>> maybeArray) {
        printf("C++ neatoMethod (consuming provided array)\n");
        int j = 0;
        for (auto i = maybeArray.begin(); i != maybeArray.end(); i++, j++) {
            printf("maybe %02d: ", j);
            if (i->has_value()) {
                printf("%d\n", i->value());
            }
            else {
                printf("(none)\n");
            }
        }
    }
    std::vector<std::optional<int32_t>> returnsArray()
    {
        std::optional<int32_t> nothing;
        std::vector<std::optional<int32_t>> values;
        values.push_back(1);
        values.push_back(nothing);
        values.push_back(3);
        values.push_back(nothing);
        values.push_back(nothing);
        values.push_back(6);
        values.push_back(7);
        values.push_back(nothing);
        return values;
    }

    //void doSomething(std::optional<std::string> maybeString, result::Result<int, std::string> result)
    //{
    //    printf("C++ doSomething:\n");
    //    printf("====== maybeString ========\n");
    //    if (maybeString.has_value()) {
    //        printf(" - hasValue: %s\n", maybeString.value().c_str());
    //    }
    //    else {
    //        printf(" - (no value)\n");
    //    }

    //    printf("====== result =============\n");
    //    if (result.is_ok()) {
    //        printf(" - result OK: %d\n", result.unwrap());
    //    }
    //    else {
    //        printf(" - result Error: %s\n", result.unwrap_err().c_str());
    //    }
    //}
}
