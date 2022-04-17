#include "NativeImplServer.h"
#include <functional>
#include <memory>
#include <string>
#include <vector>
#include <map>

typedef int CoolDelegate(std::string x);

class CoolException {
public:
    int x;
    std::string woot;
    CoolException(int x, std::string woot) : x(x), woot(woot) {}
};

typedef std::map<std::string,int> StringIntMap;

typedef struct {
    int findMe;
} DeepStruct;

typedef struct {
    int x;
    StringIntMap myMap;
    std::function<CoolDelegate> someFunc;
    DeepStruct sigh;
} Whatever;

typedef std::map<std::string,Whatever> DeepMap;

typedef void FinalDelegate(DeepMap v);

class Heyas {
public:
    virtual void someMethod(int x) = 0;  // throws CoolException
};

class ServerHeyas : public Heyas, public ServerObject {
public:
    virtual ~ServerHeyas() {
    }
    static std::shared_ptr<ServerHeyas> getByID(int id) {
        auto obj = ServerObject::getByID(id);
        return std::static_pointer_cast<ServerHeyas>(obj);
    }
};

void mapTest(std::function<FinalDelegate> whoa);
