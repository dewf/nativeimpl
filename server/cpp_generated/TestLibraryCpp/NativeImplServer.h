#pragma once

#include "CoreStuff.h"

#include <memory>
#include <functional>
#include <vector>

class ClientFunc {
protected:
	int id;
public:
	ClientFunc(int id)
		:id(id) {}
	void remoteExec();
	void remoteExecWithExceptions();
	virtual ~ClientFunc(); // will release remote (decreate remote refcount)
};

class Pushable {
public:
	virtual void push(std::shared_ptr<Pushable> outer, bool isReturn) = 0;
};

class ServerResource : public Pushable {
public:
	int id = -1;
	void push(std::shared_ptr<Pushable> outer, bool isReturn) override;
protected:
	static std::shared_ptr<ServerResource> getResourceById(int id);
	virtual void nativePush() = 0; // implemented specifically for: objects, buffers, etc
};

class ServerObject : public ServerResource {
public:
	static std::shared_ptr<ServerObject> getByID(int id);
	void nativePush() override;
};

class ClientObject : public Pushable {
private:
	int id;
public:
	ClientObject(int id) : id(id) {}
	void invokeMethod(ni_InterfaceMethodRef method);
	void invokeMethodWithExceptions(ni_InterfaceMethodRef method);
	void push(std::shared_ptr<Pushable> outer, bool isReturn) override;
	virtual ~ClientObject();
};

// our "exports" back to core (well, the DLL being built around core)
extern "C" {
	int nativeLibraryInit();
	void nativeLibraryShutdown();
	void ni_dumpTables();
	void ni_execServerFunc(int id);
	ni_ExceptionRef ni_execServerFuncWithExceptions(int id);
	void ni_releaseServerFunc(int id);
	void ni_releaseServerInst(int id);
	void ni_clearServerSafetyArea();
}

void pushServerFuncVal(std::function<void()> wrapper, size_t key);

void pushStringInternal(std::string str);
std::string popStringInternal();

void pushBoolArrayInternal(std::vector<bool> values);
std::vector<bool> popBoolArrayInternal();

void pushIntArrayInternal(std::vector<int> values);
std::vector<int> popIntArrayInternal();

void pushStringArrayInternal(std::vector<std::string> values);
std::vector<std::string> popStringArrayInternal();

void pushSizeTArrayInternal(std::vector<size_t> values);
std::vector<size_t> popSizeTArrayInternal();
