#include "NativeImplServer.h"
#include "Implementation.h"

// enum pushes =========================

void CoolEnum__push(CoolEnum value) {
	switch (value.tag) {
	case CoolEnum::Tag::Value1:
		// no args
		break;
	case CoolEnum::Tag::Value2:
		ni_pushInt(value.value2->x);
		break;
	case CoolEnum::Tag::Value3:
		pushIntArrayInternal(value.value3->things);
		pushStringInternal(value.value3->m);
		break;
	}
	ni_pushInt((int)value.tag);
}

CoolEnum CoolEnum__pop() {
	auto which = (CoolEnum::Tag)ni_popInt();
	switch (which) {
	case CoolEnum::Tag::Value1: {
		// no args
		return CoolEnum::Value1::make(); }
	case CoolEnum::Tag::Value2: {
		auto x = ni_popInt();
		return CoolEnum::Value2::make(x); }
	case CoolEnum::Tag::Value3: {
		auto m = popStringInternal();
		auto things = popIntArrayInternal();
		return CoolEnum::Value3::make(m, things); }
	default:
		throw "CoolEnum__pop(): unknown tag!";
	}
}

// ===========================

void someFunction__wrapper() {
    auto value = CoolEnum__pop();
    ni_pushInt(someFunction(value));
}

extern "C" int nativeLibraryInit() {
    auto m = ni_registerModule("TestLibrary");
    ni_registerModuleMethod(m, "someFunction", &someFunction__wrapper);
    return 0; // = OK
}

extern "C" void nativeLibraryShutdown() {
}
