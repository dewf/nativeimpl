#include "Implementation.h"

int32_t sumValues(std::vector<int32_t> values, std::string threadId) {
    //printf("C++ sumValues [tid: %s]\n", threadId.c_str());
    int sum = 0;
    for (auto i = values.begin(); i != values.end(); i++) {
        sum += *i;
    }
    return sum;
}
