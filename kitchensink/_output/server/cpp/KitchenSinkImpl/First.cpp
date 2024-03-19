#include "generated/First.h"

namespace First
{
    void method1(std::vector<result::Result<int32_t, std::string>> resultArray) {
        printf("C++ method1 param:\n");
        for (auto i = resultArray.begin(); i != resultArray.end(); i++) {
            if (i->is_ok()) {
                printf(" - OK: %d\n", i->unwrap());
            }
            else {
                printf(" - err: %s\n", i->unwrap_err().c_str());
            }
        }
    }
    std::vector<result::Result<int32_t, std::string>> method2() {
        std::vector<result::Result<int32_t, std::string>> ret;
        ret.push_back(result::Ok(1));
        ret.push_back(result::Err(std::string("two")));
        ret.push_back(result::Ok(3));
        ret.push_back(result::Err(std::string("four")));
        ret.push_back(result::Ok(5));
        return ret;
    }
}
