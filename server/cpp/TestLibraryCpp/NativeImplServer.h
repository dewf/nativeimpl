#pragma once

#include "CoreStuff.h"

#include <memory>
#include <functional>

class ClientFunc {
protected:
	int id;
public:
	ClientFunc(int id)
		:id(id) {}
	void remoteExec(); // remote exec
	virtual ~ClientFunc(); // will release remote (decreate remote refcount)
};

class Pushable {
public:
	virtual void push(std::shared_ptr<Pushable> outer, bool isReturn) = 0;
};

class ServerObject : public Pushable {
public:
	int id = -1;
	static std::shared_ptr<ServerObject> getByID(int id);
	void push(std::shared_ptr<Pushable> outer, bool isReturn) override;
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
	void ni_releaseServerFunc(int id);
	void ni_releaseServerInst(int id);
	void ni_clearServerSafetyArea();
}

void pushServerFuncVal(std::function<void()> wrapper, size_t key);
void pushStringInternal(std::string str);
std::string popStringInternal();
