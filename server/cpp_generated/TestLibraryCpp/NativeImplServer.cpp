#include "NativeImplServer.h"
#include <functional>
#include <map>
#include <memory>
#include <vector>
#include <assert.h>

struct ServerFuncVal {
	int id;
	std::function<void()> wrapper;
	size_t uniqueKey;
	int refCount;
};
static int nextServerFuncId = 1;
static std::map<int, ServerFuncVal> serverFuncVals;
static std::map<size_t, int> reverseServerFuncVals;

struct ServerObjectInfo {
	int id;
	std::shared_ptr<ServerObject> object;
	int refCount;
};
static int nextServerObjId = 1;
static std::map<int, ServerObjectInfo> serverObjects;

void ni_dumpTables() {
	printf("== C++ server func vals:\n");
	for (auto i = serverFuncVals.begin(); i != serverFuncVals.end(); i++) {
		printf(" - func %d: refcount %d\n", i->first, i->second.refCount);
	}
	printf("== C++ server objects:\n");
	for (auto i = serverObjects.begin(); i != serverObjects.end(); i++) {
		printf(" - obj %d: refcount %d\n", i->first, i->second.refCount);
	}
}

void ni_execServerFunc(int id)
{
	serverFuncVals[id].wrapper();
}

void ni_releaseServerFunc(int id)
{
	serverFuncVals[id].refCount -= 1;
	if (serverFuncVals[id].refCount == 0) {
		printf("server releaseFuncVal(%d): refcount 0, releasing\n", id);
		auto key = serverFuncVals[id].uniqueKey;
		reverseServerFuncVals.erase(key);
		serverFuncVals.erase(id);
	}
}

int addFuncVal(std::function<void()> wrapper, size_t key)
{
	if (key != 0) {
		auto found = reverseServerFuncVals.find(key);
		if (found != reverseServerFuncVals.end()) {
			// increment refcount of existing
			auto id = found->second;
			serverFuncVals[id].refCount++;
			return id;
		}
	}
	// either no key provided, or not found: create anew
	auto id = nextServerFuncId++;
	serverFuncVals[id] = ServerFuncVal{ id, wrapper, key, 1 };
	if (key != 0) {
		// no point stuffing keyless things in the reverse map, all on top of each other!
		reverseServerFuncVals[key] = id;
	}
	return id;
}

void pushServerFuncVal(std::function<void()> wrapper, size_t key) {
	auto id = addFuncVal(wrapper, key);
	ni_pushServerFunc(id);
};

void ServerObject::push(std::shared_ptr<Pushable> outer, bool isReturn) {
	// isReturn not used for server stuff, that's just to help out with the safe area / protecting client objects
	if (id == -1) { // unset as of yet
		id = nextServerObjId++;
		auto serverOuter = std::dynamic_pointer_cast<ServerObject>(outer);
		serverObjects[id] = ServerObjectInfo{ id, serverOuter, 1 };
		//printf("ServerObject::soPush()ed into serverObjects[%d]\n", id);
	}
	else {
		// add ref to existing
		serverObjects[id].refCount++; // this assumes that it's going across a boundary which will be balanced by an eventual deref. is that always true?
		printf("bumped server ID %d refcount to %d\n", id, serverObjects[id].refCount);
	}
	ni_pushServerInst(id);
}

std::shared_ptr<ServerObject> ServerObject::getByID(int id) {
	return serverObjects[id].object;
}

void ClientFunc::remoteExec()
{
	ni_clientFuncExec(id);
}

ClientFunc::~ClientFunc()
{
	// release remote function, we're done with it
	// (only reduces remote refcount - there may be other client refCounts referring to separate ClientFuncs on this side!)
	// although I think a weak_ptr table (id -> weak_ptr) will get rid of that issue, deduplicating for us!
	printf("c++: ClientFunc dtor releasing func id: %d\n", id);
	fflush(stdout);
	ni_clientFuncRelease(id);
}

void ni_releaseServerInst(int id)
{
	serverObjects[id].refCount -= 1;
	if (serverObjects[id].refCount == 0) {
		printf("C++: releasing server obj %d\n", id);
		serverObjects.erase(id);
	}
	else {
		printf("C++: releaseServerInstID(%d) - refCount still %d\n", id, serverObjects[id].refCount);
	}
}

// while server objects are kept alive ("checked out") via the serverObjects/refcount,
// any client objects that currently only exist on this side, need to be protected during returns to keep their shared_ptr from going away
// and releasing them prematurely (dropping remote refcount to 0)
// the other side will call clearServerSafetyArea when it's gotten the return value
// each side has its own safety area, to protect the other's resources during function return handoff
static std::vector<std::shared_ptr<ClientObject>> safeArea;

// clear server resources from safety area (protects values being returned with no other shared_ptrs existing on this side)
void ni_clearServerSafetyArea() {
	printf("C++ before safety clear =====\n");
	safeArea.clear();
	printf("C++ after safety clear!! ====\n");
}

void ClientObject::invokeMethod(ni_InterfaceMethodRef method) {
	printf("C++ ClientObject::invokeMethod(%p) on id: %d\n", method, id);
	::ni_clientMethodExec(method, id);
	assert(ni_currentException == nullptr); // never use this where an exception can throw! (trying to keep things tight for maximum throughput)
}

void ClientObject::invokeMethodWithExceptions(ni_InterfaceMethodRef method) {
	printf("C++ ClientObject::invokeMethodWithExceptions(%p) on id: %d\n", method, id);
	assert(ni_currentException == nullptr);
	::ni_clientMethodExec(method, id);
	if (ni_currentException != nullptr) {
		auto e = ni_currentException;
		ni_currentException = nullptr;
		ni_buildAndthrow(e);
	}
}

void ClientObject::push(std::shared_ptr<Pushable> outer, bool isReturn) {
	if (isReturn) {
		// for return values, to keep them alive until other side has taken them
		printf("C++ ClientObject %d safepush\n", id);
		ni_pushClientInst(id);
		// preserve for return values etc
		auto clientOuter = std::dynamic_pointer_cast<ClientObject>(outer);
		safeArea.push_back(clientOuter);
	}
	else {
		ni_pushClientInst(id);
	}
}

ClientObject::~ClientObject() {
	printf("C++ ClientObject::dtor, remote releasing\n");
	::ni_clientObjRelease(id);
}
