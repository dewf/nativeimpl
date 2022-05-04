#pragma once

#include "CoreStuff.h"

#include <memory>
#include <functional>
#include <vector>

class Pushable {
public:
	virtual void push(std::shared_ptr<Pushable> outer, bool isReturn) = 0;
};

class ServerResource : public Pushable {
private:
	int refCount = 0;
protected:
	int id = -1;
public:
	void release();
	void push(std::shared_ptr<Pushable> outer, bool isReturn) override;
	~ServerResource();

	static std::shared_ptr<ServerResource> getResourceById(int id);
	static void dumpTables();
protected:
	virtual void nativePush() = 0; // implemented specifically for: objects, buffers, etc
	virtual void releaseExtra() {} // anything else you might want to do here, when destroyed (eg used by ServerFuncVal to remove itself from reverse map)
};

class ClientResource : public Pushable {
protected:
	int id;
	virtual void nativePush(bool inSafetyArea) = 0;
public:
	ClientResource(int id) : id(id) {}
	void push(std::shared_ptr<Pushable> outer, bool isReturn) override;
	virtual ~ClientResource();
};

class ServerObject : public ServerResource {
public:
	static std::shared_ptr<ServerObject> getByID(int id);
protected:
	void nativePush() override;
};

class ClientObject : public ClientResource {
public:
	ClientObject(int id) : ClientResource(id) {}
	void invokeMethod(ni_InterfaceMethodRef method);
	void invokeMethodWithExceptions(ni_InterfaceMethodRef method);
protected:
	void nativePush(bool inSafetyArea) override;
};

class ClientFuncVal : public ClientResource {
public:
	ClientFuncVal(int id) : ClientResource(id) {}
	void remoteExec();
	void remoteExecWithExceptions();
protected:
	void nativePush(bool inSafetyArea) override;
};

class ServerFuncVal : public ServerResource {
private:
	ServerFuncVal(std::function<void()> wrapper, size_t uniqueKey)
		: wrapper(wrapper), uniqueKey(uniqueKey) {}
	size_t uniqueKey;
public:
	std::function<void()> wrapper;
	void nativePush() override;
	static std::shared_ptr<ServerFuncVal> getById(int id);
	static std::shared_ptr<ServerFuncVal> create(std::function<void()> wrapper, size_t key);
protected:
	void releaseExtra() override;
};

template <typename T>
class NativeBuffer {
public:
	virtual T* getSpan(size_t* length) = 0;
};

template <typename T>
class ClientBuffer : public ClientResource, public NativeBuffer<T> {
private:
	ni_BufferDescriptor descriptor;
public:
	ClientBuffer(int id, ni_BufferDescriptor& descriptor)
		: ClientResource(id), descriptor(descriptor) {}
	T* getSpan(size_t* length) override {
		*length = descriptor.totalCount;
		return (T*)descriptor.start;
	}
	void nativePush(bool inSafetyArea) override {
		ni_pushBuffer(id, true, inSafetyArea, &descriptor);
	}
};


// our "exports" back to core (well, the DLL being built around core)
extern "C" {
	int nativeLibraryInit();
	void nativeLibraryShutdown();
	void ni_dumpTables();
	void ni_execServerFunc(int id);
	ni_ExceptionRef ni_execServerFuncWithExceptions(int id);
	void ni_releaseServerResource(int id);
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
