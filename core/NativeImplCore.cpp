#include "NativeImplCore.h"

#include <cstdio>
#include <map>
#include <string>
#include <stack>
#include <exception>
#include <functional>
#include <memory>
#include <vector>
#include <assert.h>

// private opaque definitions
struct ni_Module {
	std::string name;
	std::map<std::string, ni_ModuleMethodRef> methods;
	std::map<std::string, ni_InterfaceRef> interfaces;
	std::map<std::string, ni_ExceptionRef> exceptions;
	ConstantsFunc constantsFunc = nullptr;
};

struct ni_ModuleMethod {
	ni_ModuleRef _module = nullptr;
	std::string name;
	ModuleFunc func;
};

struct ni_Interface {
	ni_ModuleRef _module = nullptr;
	std::string name;
	std::map<std::string, ni_InterfaceMethodRef> methods;
};

struct ni_InterfaceMethod {
	ni_InterfaceRef _iface = nullptr;
	std::string name;
	InterfaceFunc ifunc;
};

struct ni_Exception {
	ni_ModuleRef _module = nullptr;
	std::string name;
	ExceptionBuilder buildAndThrow;
};

// exceptions
class WrongTypeException : public std::exception {
	virtual const char* what() const throw() {
		return "Wrong Stack Item Type";
	}
};

// stack types =======================
class StackItem {
public:
	virtual bool boolValue() { throw WrongTypeException(); }
	virtual std::vector<uint8_t> boolArray() { throw WrongTypeException(); } // using 'bool' doesn't allow .data access
	virtual int intValue() { throw WrongTypeException(); }
	virtual std::vector<int> intArray() { throw WrongTypeException(); }
	virtual std::string stringValue() { throw WrongTypeException(); }
	virtual std::vector<std::string> stringArray() { throw WrongTypeException(); }
	virtual int clientFuncId() { throw WrongTypeException(); }
	virtual int serverFuncId() { throw WrongTypeException(); }
	virtual int instanceId(bool* isClientId) { throw WrongTypeException(); }
};

class BoolItem : public StackItem {
public:
	bool value;
	BoolItem(bool value) : value(value) {}
	bool boolValue() override {
		return value;
	}
};

class BoolArrayItem : public StackItem {
public:
	std::vector<uint8_t> values;		// using 'bool' doesn't allow .data access, lame
	BoolArrayItem(bool* ptr, size_t count) {
		values.assign(ptr, ptr + count);
	}
	std::vector<uint8_t> boolArray() override {
		return values;
	}
};

class IntItem : public StackItem {
public:
	int x;
	IntItem(int x) : x(x) {}
	int intValue() override {
		return x;
	}
};

class IntArrayItem : public StackItem {
public:
	std::vector<int> values;
	IntArrayItem(int* ptr, size_t count) {
		values.assign(ptr, ptr + count);
	}
	std::vector<int> intArray() override {
		return values;
	}
};

class StringItem : public StackItem {
public:
	std::string str;
	StringItem(const char* raw, size_t length) {
		if (length > 0) {
			str.assign(raw, length);
		}
		else {
			str = raw;
		}
	}
	std::string stringValue() override {
		return str;
	}
};

class StringArrayItem : public StackItem {
public:
	std::vector<std::string> values;
	StringArrayItem(const char** ptrs, size_t* lengths, size_t count) {
		for (auto i = 0; i < count; i++) {
			std::string str;
			if (lengths[i] > 0) {
				str.assign(ptrs[i], lengths[i]);
			}
			else {
				str = ptrs[i]; // null-terminated
			}
			values.push_back(str);
		}
	}
	std::vector<std::string> stringArray() override {
		return values;
	}
};

// client func val
class ClientFuncItem : public StackItem {
public:
	int id;
	ClientFuncItem(int id) : id(id) {}
	int clientFuncId() override {
		return id;
	}
};

// server func val
class ServerFuncItem : public StackItem {
public:
	int id;
	ServerFuncItem(int id) : id(id) {}
	int serverFuncId() override {
		return id;
	}
};

// client inst ID
class ClientInstItem : public StackItem {
public:
	int id;
	ClientInstItem(int id) : id(id) {}
	int instanceId(bool* isClientId) override {
		*isClientId = true;
		return id;
	}
};

class ServerInstItem : public StackItem {
public:
	int id;
	ServerInstItem(int id) : id(id) {}
	int instanceId(bool* isClientId) override {
		*isClientId = false;
		return id;
	}
};

// null methods for callback defaults (also assigned when client is shut down, to prevent dtors from calling back into client)
static void nullIntMethod(int x) {}
static void nullVoidMethod() {}
static void nullMethodExec(ni_InterfaceMethodRef method, int id) {}

// private global stuff ============================
static std::map<std::string, ni_ModuleRef> modules;
static std::stack<StackItem *> vstack;

niClientFuncExec ni_clientFuncExec;
niClientFuncRelease ni_clientFuncRelease;
niClientMethodExec ni_clientMethodExec;
niClientObjRelease ni_clientObjRelease;
niClientClearSafetyArea ni_clientClearSafetyArea;

ni_ExceptionRef ni_currentException = nullptr;

ni_ModuleRef ni_registerModule(const char *name)
{
	// make sure name doesn't already exist
	auto found = modules.find(name);
	if (found != modules.end()) {
		printf("registerModule: module name [%s] already exists!\n", name);
		return nullptr;
	}
	auto mod = new ni_Module{ name };
	modules[name] = mod;
	return mod;
}

ni_ModuleMethodRef ni_registerModuleMethod(ni_ModuleRef m, const char *name, ModuleFunc func)
{
	auto found = m->methods.find(name);
	if (found != m->methods.end()) {
		printf("registerModuleMethod: method name [%s] already exists!\n", name);
		return nullptr;
	}
	auto method = new ni_ModuleMethod{ m, name, func };
	m->methods[name] = method;
	return method;
}

void ni_registerModuleConstants(ni_ModuleRef m, ConstantsFunc func) {
	if (m->constantsFunc != nullptr) {
		printf("Module %s ConstantsFunc already defined\n", m->name.c_str());
	}
	else {
		m->constantsFunc = func;
	}
}

ni_InterfaceRef ni_registerInterface(ni_ModuleRef m, const char *name)
{
	auto found = m->interfaces.find(name);
	if (found != m->interfaces.end()) {
		printf("registerModuleInterface: interface name [%s] already exists!\n", name);
		return nullptr;
	}
	auto iface = new ni_Interface{ m, name };
	m->interfaces[name] = iface;
	return iface;
}

ni_InterfaceMethodRef ni_registerInterfaceMethod(ni_InterfaceRef iface, const char *name, InterfaceFunc ifunc)
{
	auto found = iface->methods.find(name);
	if (found != iface->methods.end()) {
		printf("registerInterfaceMethod: iface method name [%s] already exists!\n", name);
		return nullptr;
	}
	auto method = new ni_InterfaceMethod{ iface, name, ifunc };
	iface->methods[name] = method;
	return method;
}

ni_ExceptionRef ni_registerException(ni_ModuleRef m, const char *name, ExceptionBuilder buildAndThrow) {
	auto found = m->exceptions.find(name);
	if (found != m->exceptions.end()) {
		printf("registerException: method exception name [%s] already exists!\n", name);
		return nullptr;
	}
	auto exception = new ni_Exception{ m, name, buildAndThrow };
	m->exceptions[name] = exception;
	return exception;
}

void ni_pushServerFunc(int id)
{
	vstack.push(new ServerFuncItem(id));
}

// client methods (some used by server as well) ===============================

int ni_nativeImplInit(
	niClientFuncExec clientFuncExec,
	niClientFuncRelease clientFuncRelease,
	niClientMethodExec clientMethodExec,
	niClientObjRelease clientObjRelease,
	niClientClearSafetyArea clientClearSafetyArea
	)
{
	::ni_clientFuncExec = clientFuncExec;
	::ni_clientFuncRelease = clientFuncRelease;
	::ni_clientMethodExec = clientMethodExec;
	::ni_clientObjRelease = clientObjRelease;
	::ni_clientClearSafetyArea = clientClearSafetyArea;

	// library-specific registrations etc
	return nativeLibraryInit();
}

void ni_nativeImplShutdown()
{
	printf("nativeImplShutdown\n");

	// de-fang the callbacks so they don't wreak havoc
	ni_clientFuncExec = &nullIntMethod;
	ni_clientFuncRelease = &nullIntMethod;
	ni_clientMethodExec = &nullMethodExec;
	ni_clientObjRelease = &nullIntMethod;
	ni_clientClearSafetyArea = &nullVoidMethod;

	printf(" - callbacks disabled\n");
	fflush(stdout);

	nativeLibraryShutdown();
}

ni_ModuleRef ni_getModule(const char* name) {
	auto found = modules.find(std::string(name));
	if (found != modules.end()) {
		return found->second;
	}
	return nullptr;
}

ni_ModuleMethodRef ni_getModuleMethod(ni_ModuleRef m, const char* name) {
	auto found = m->methods.find(std::string(name));
	if (found != m->methods.end()) {
		return found->second;
	}
	return nullptr;
}

ni_InterfaceRef ni_getInterface(ni_ModuleRef m, const char* name) {
	auto found = m->interfaces.find(std::string(name));
	if (found != m->interfaces.end()) {
		return found->second;
	}
	return nullptr;
}

ni_InterfaceMethodRef ni_getInterfaceMethod(ni_InterfaceRef iface, const char* name) {
	auto found = iface->methods.find(std::string(name));
	if (found != iface->methods.end()) {
		return found->second;
	}
	return nullptr;
}

ni_ExceptionRef ni_getException(ni_ModuleRef m, const char* name) {
	auto found = m->exceptions.find(std::string(name));
	if (found != m->exceptions.end()) {
		return found->second;
	}
	return nullptr;
}

void ni_pushModuleConstants(ni_ModuleRef m) {
	m->constantsFunc();
}

void ni_invokeModuleMethod(ni_ModuleMethodRef method) {
	method->func();
	assert(ni_currentException == nullptr); // this should never be used for methods potentially throwing exceptions!
}

ni_ExceptionRef ni_invokeModuleMethodWithExceptions(ni_ModuleMethodRef method) {
	assert(ni_currentException == nullptr);
	method->func();
	auto ret = ni_currentException;
	ni_currentException = nullptr;
	return ret;
}

void ni_pushBool(bool value) {
	vstack.push(new BoolItem(value));
}

bool ni_popBool() {
	auto item = vstack.top();
	auto b = item->boolValue();
	vstack.pop();
	delete item;
	return b;
}

void ni_pushBoolArray(bool* values, size_t count)
{
	vstack.push(new BoolArrayItem(values, count));
}

static std::vector<uint8_t> lastBoolArray;

void ni_popBoolArray(bool** values, size_t* count)
{
	auto item = vstack.top();
	lastBoolArray = item->boolArray();
	vstack.pop();
	delete item;
	*((uint8_t**)values) = lastBoolArray.data();
	*count = lastBoolArray.size();
}

void ni_pushInt(int x) {
	vstack.push(new IntItem(x));
}

int ni_popInt() {
	auto item = vstack.top();
	auto x = item->intValue();
	vstack.pop();
	delete item;
	return x;
}

void ni_pushIntArray(int* values, size_t count)
{
	vstack.push(new IntArrayItem(values, count));
}

static std::vector<int> lastIntArray;

void ni_popIntArray(int** values, size_t* count) {
	auto item = vstack.top();
	lastIntArray = item->intArray();
	vstack.pop();
	delete item;
	*values = lastIntArray.data();
	*count = lastIntArray.size();
}

void ni_pushString(const char* str, size_t length) {
	vstack.push(new StringItem(str, length));
}

static std::string lastPoppedString; // pointers gotta point to something ...

void ni_popString(const char** strPtr, size_t* length) {
	auto item = vstack.top();
	lastPoppedString = item->stringValue();
	vstack.pop();
	delete item;
	*strPtr = lastPoppedString.c_str();
	*length = lastPoppedString.size();
}

void ni_pushStringArray(const char** strs, size_t* lengths, size_t count)
{
	vstack.push(new StringArrayItem(strs, lengths, count));
}

static std::vector<std::string> lastPoppedStringArray;
static const char** lastPoppedStrs = nullptr;
static size_t* lastPoppedStrLengths = nullptr;

void ni_popStringArray(const char*** strs, size_t** lengths, size_t* count)
{
	auto item = vstack.top();
	lastPoppedStringArray = item->stringArray();
	vstack.pop();
	delete item;
	//
	delete[] lastPoppedStrs;
	delete[] lastPoppedStrLengths;
	auto _count = lastPoppedStringArray.size();
	lastPoppedStrs = new const char*[_count];
	lastPoppedStrLengths = new size_t[_count];
	for (auto i = 0; i < _count; i++) {
		lastPoppedStrs[i] = lastPoppedStringArray[i].data();
		lastPoppedStrLengths[i] = lastPoppedStringArray[i].size();
	}
	*strs = lastPoppedStrs;
	*lengths = lastPoppedStrLengths;
	*count = _count;
}

// visible to client ===========================================
void ni_pushClientFunc(int id)
{
	vstack.push(new ClientFuncItem(id));
}

int ni_popServerFunc() {
	auto item = vstack.top();
	auto id = item->serverFuncId();
	vstack.pop();
	delete item;
	return id;
}

void ni_invokeInterfaceMethod(ni_InterfaceMethodRef method, int serverID)
{
	method->ifunc(serverID);
	assert(ni_currentException == nullptr); // use below when exceptions are involved
}

ni_ExceptionRef ni_invokeInterfaceMethodWithExceptions(ni_InterfaceMethodRef method, int serverID) {
	assert(ni_currentException == nullptr);
	method->ifunc(serverID);
	auto ret = ni_currentException;
	ni_currentException = nullptr;
	return ret;
}

// used both internally and externally
void ni_pushClientInst(int id) {
	vstack.push(new ClientInstItem(id));
}

void ni_pushServerInst(int id) {
	vstack.push(new ServerInstItem(id));
}

int ni_popInstance(bool* isClientID) {
	auto item = vstack.top();
	auto id = item->instanceId(isClientID);
	vstack.pop();
	delete item;
	return id;
}

void ni_setException(ni_ExceptionRef e) {
	ni_currentException = e;
}

// visible to server only ====================
int ni_popClientFunc() {
	auto item = vstack.top();
	auto id = item->clientFuncId();
	vstack.pop();
	delete item;
	return id;
}

void ni_clearClientSafetyArea() {
	::ni_clientClearSafetyArea(); // ugh this confusing naming ...
}

void ni_buildAndthrow(ni_ExceptionRef e) {
	e->buildAndThrow();
}
