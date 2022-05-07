#include "NativeImplServer.h"
#include "Implementation.h"

ni_InterfaceMethodRef windowDelegate_onDestroyed;
ni_InterfaceMethodRef windowDelegate_onResized;



inline void KeyEnum__push(KeyEnum value) {
    ni_pushInt((int)value);
}

inline KeyEnum KeyEnum__pop() {
    auto tag = ni_popInt();
    return (KeyEnum)tag;
}




class ClientWindowDelegate : public ClientObject, public WindowDelegate {
public:
    ClientWindowDelegate(int id) : ClientObject(id) {}
    void onDestroyed() override {
        invokeMethod(windowDelegate_onDestroyed);
    }
    void onResized(int newWidth, int newHeight) override {
        ni_pushInt(newHeight);
        ni_pushInt(newWidth);
        invokeMethod(windowDelegate_onResized);
    }
};

inline void WindowDelegate__push(std::shared_ptr<WindowDelegate> inst, bool isReturn) {
    auto pushable = std::dynamic_pointer_cast<Pushable>(inst);
    pushable->push(pushable, isReturn);
}

std::shared_ptr<WindowDelegate> WindowDelegate__pop()
{
    bool isClientID, inSafetyArea;
    auto id = ni_popInstance(&isClientID, &inSafetyArea);
    if (isClientID) {
        return std::shared_ptr<WindowDelegate>(new ClientWindowDelegate(id));
    }
    else {
        return ServerWindowDelegate::getByID(id);
    }
}

void createWindow__wrapper() {
    auto title = popStringInternal();
    auto del = WindowDelegate__pop();
    ni_pushSizeT((size_t)createWindow(title, del));
}

void windowShow__wrapper() {
    auto w = (Window)ni_popSizeT();
    windowShow(w);
}

void windowSetMenuBar__wrapper() {
    auto w = (Window)ni_popSizeT();
    auto mb = (MenuBar)ni_popSizeT();
    windowSetMenuBar(w, mb);
}

void windowDestroy__wrapper() {
    auto w = (Window)ni_popSizeT();
    windowDestroy(w);
}

void createAccelerator__wrapper() {
    auto key = KeyEnum__pop();
    ni_pushSizeT((size_t)createAccelerator(key));
}

void createAction__wrapper() {
    auto id = ni_popInt();
    auto label = popStringInternal();
    auto acc = (Accelerator)ni_popSizeT();
    ni_pushSizeT((size_t)createAction(id, label, acc));
}

void createMenu__wrapper() {
    ni_pushSizeT((size_t)createMenu());
}

void menuAddAction__wrapper() {
    auto menu = (Menu)ni_popSizeT();
    auto action = (Action)ni_popSizeT();
    menuAddAction(menu, action);
}

void menuAddSubmenu__wrapper() {
    auto menu = (Menu)ni_popSizeT();
    auto label = popStringInternal();
    auto sub = (Menu)ni_popSizeT();
    menuAddSubmenu(menu, label, sub);
}

void menuAddSeparator__wrapper() {
    auto menu = (Menu)ni_popSizeT();
    menuAddSeparator(menu);
}

void createMenuBar__wrapper() {
    ni_pushSizeT((size_t)createMenuBar());
}

void menuBarAddMenu__wrapper() {
    auto mb = (MenuBar)ni_popSizeT();
    auto label = popStringInternal();
    auto menu = (Menu)ni_popSizeT();
    menuBarAddMenu(mb, label, menu);
}

void runloop__wrapper() {
    ni_pushInt(runloop());
}

void exitRunloop__wrapper() {
    exitRunloop();
}

void moduleInit__wrapper() {
    ni_pushInt(moduleInit());
}

void moduleShutdown__wrapper() {
    moduleShutdown();
}

void WindowDelegate_onDestroyed__wrapper(int serverID) {
    auto inst = ServerWindowDelegate::getByID(serverID);
    inst->onDestroyed();
}

void WindowDelegate_onResized__wrapper(int serverID) {
    auto inst = ServerWindowDelegate::getByID(serverID);
    auto newWidth = ni_popInt();
    auto newHeight = ni_popInt();
    inst->onResized(newWidth, newHeight);
}

extern "C" int nativeLibraryInit() {
    auto m = ni_registerModule("TestLibrary");
    ni_registerModuleMethod(m, "createWindow", &createWindow__wrapper);
    ni_registerModuleMethod(m, "windowShow", &windowShow__wrapper);
    ni_registerModuleMethod(m, "windowSetMenuBar", &windowSetMenuBar__wrapper);
    ni_registerModuleMethod(m, "windowDestroy", &windowDestroy__wrapper);
    ni_registerModuleMethod(m, "createAccelerator", &createAccelerator__wrapper);
    ni_registerModuleMethod(m, "createAction", &createAction__wrapper);
    ni_registerModuleMethod(m, "createMenu", &createMenu__wrapper);
    ni_registerModuleMethod(m, "menuAddAction", &menuAddAction__wrapper);
    ni_registerModuleMethod(m, "menuAddSubmenu", &menuAddSubmenu__wrapper);
    ni_registerModuleMethod(m, "menuAddSeparator", &menuAddSeparator__wrapper);
    ni_registerModuleMethod(m, "createMenuBar", &createMenuBar__wrapper);
    ni_registerModuleMethod(m, "menuBarAddMenu", &menuBarAddMenu__wrapper);
    ni_registerModuleMethod(m, "runloop", &runloop__wrapper);
    ni_registerModuleMethod(m, "exitRunloop", &exitRunloop__wrapper);
    ni_registerModuleMethod(m, "moduleInit", &moduleInit__wrapper);
    ni_registerModuleMethod(m, "moduleShutdown", &moduleShutdown__wrapper);
    auto windowDelegate = ni_registerInterface(m, "WindowDelegate");
    windowDelegate_onDestroyed = ni_registerInterfaceMethod(windowDelegate, "onDestroyed", &WindowDelegate_onDestroyed__wrapper);
    windowDelegate_onResized = ni_registerInterfaceMethod(windowDelegate, "onResized", &WindowDelegate_onResized__wrapper);
    return 0; // = OK
}

extern "C" void nativeLibraryShutdown() {
}
