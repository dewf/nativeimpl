#include "NativeImplServer.h"
#include <functional>
#include <memory>
#include <string>
#include <vector>
#include <map>
#include <tuple>

typedef int IntFunc(int x);

class MyThing {
public:
    virtual int doSomething(int x) = 0;
};

// inherit from this to create server instances of MyThing
class ServerMyThing : public MyThing, public ServerObject {
public:
    virtual ~ServerMyThing() {}
    static std::shared_ptr<ServerMyThing> getByID(int id) {
        auto obj = ServerObject::getByID(id);
        return std::static_pointer_cast<ServerMyThing>(obj);
    }
};

void takesThing(std::shared_ptr<MyThing> thing);
std::shared_ptr<MyThing> returnsThing();
void applyFunc(std::function<IntFunc> f);
void takesBuffer(std::shared_ptr<NativeBuffer<int>> buffer);
std::shared_ptr<NativeBuffer<int>> returnsBuffer();
