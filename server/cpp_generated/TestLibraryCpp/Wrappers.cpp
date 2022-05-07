#include "NativeImplServer.h"
#include "Implementation.h"

ni_InterfaceMethodRef windowDelegate_onDestroyed;
ni_InterfaceMethodRef window_show;

class ClientWindow : public ClientObject, public Window {
public:
    ClientWindow(int id) : ClientObject(id) {}
    void show() override {
        invokeMethod(window_show);
    }
};

inline void Window__push(std::shared_ptr<Window> inst, bool isReturn) {
    auto pushable = std::dynamic_pointer_cast<Pushable>(inst);
    pushable->push(pushable, isReturn);
}

std::shared_ptr<Window> Window__pop()
{
    bool isClientID, inSafetyArea;
    auto id = ni_popInstance(&isClientID, &inSafetyArea);
    if (isClientID) {
        return std::shared_ptr<Window>(new ClientWindow(id));
    }
    else {
        return ServerWindow::getByID(id);
    }
}

class ClientWindowDelegate : public ClientObject, public WindowDelegate {
public:
    ClientWindowDelegate(int id) : ClientObject(id) {}
    void onDestroyed() override {
        invokeMethod(windowDelegate_onDestroyed);
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
    Window__push(createWindow(title, del), true);
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

void Window_show__wrapper(int serverID) {
    auto inst = ServerWindow::getByID(serverID);
    inst->show();
}

extern "C" int nativeLibraryInit() {
    auto m = ni_registerModule("TestLibrary");
    ni_registerModuleMethod(m, "createWindow", &createWindow__wrapper);
    ni_registerModuleMethod(m, "runloop", &runloop__wrapper);
    ni_registerModuleMethod(m, "exitRunloop", &exitRunloop__wrapper);
    ni_registerModuleMethod(m, "moduleInit", &moduleInit__wrapper);
    ni_registerModuleMethod(m, "moduleShutdown", &moduleShutdown__wrapper);
    auto windowDelegate = ni_registerInterface(m, "WindowDelegate");
    windowDelegate_onDestroyed = ni_registerInterfaceMethod(windowDelegate, "onDestroyed", &WindowDelegate_onDestroyed__wrapper);
    auto window = ni_registerInterface(m, "Window");
    window_show = ni_registerInterfaceMethod(window, "show", &Window_show__wrapper);
    return 0; // = OK
}

extern "C" void nativeLibraryShutdown() {
}
