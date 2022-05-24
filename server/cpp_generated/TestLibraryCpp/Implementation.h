#include "NativeImplServer.h"
#include <functional>
#include <memory>
#include <string>
#include <vector>
#include <map>
#include <tuple>
#include <set>

class CoolThing {
public:
    virtual int32_t method1(std::string x) = 0;
};

// inherit from this to create server instances of CoolThing
class ServerCoolThing : public CoolThing, public ServerObject {
public:
    virtual ~ServerCoolThing() {}
    static std::shared_ptr<ServerCoolThing> getByID(int id) {
        auto obj = ServerObject::getByID(id);
        return std::static_pointer_cast<ServerCoolThing>(obj);
    }
};

int32_t doSomething(int32_t x, std::string y);
