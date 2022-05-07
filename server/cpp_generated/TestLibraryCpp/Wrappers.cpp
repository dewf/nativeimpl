#include "NativeImplServer.h"
#include "Implementation.h"

ni_InterfaceMethodRef windowDelegate_onDestroyed;
ni_InterfaceMethodRef window_show;
ni_InterfaceMethodRef window_setMenuBar;
ni_InterfaceMethodRef menu_addAction;
ni_InterfaceMethodRef menu_addSubmenu;
ni_InterfaceMethodRef menu_addSeparator;
ni_InterfaceMethodRef menuBar_addMenu;



inline void KeyEnum__push(KeyEnum value) {
    ni_pushInt((int)value);
}

inline KeyEnum KeyEnum__pop() {
    auto tag = ni_popInt();
    return (KeyEnum)tag;
}

void Menu__push(std::shared_ptr<Menu> thing, bool isReturn);
std::shared_ptr<Menu> Menu__pop();

class ClientMenu : public ClientObject, public Menu {
public:
    ClientMenu(int id) : ClientObject(id) {}
    void addAction(Action action) override {
        ni_pushSizeT((size_t)action);
        invokeMethod(menu_addAction);
    }
    void addSubmenu(std::string label, std::shared_ptr<Menu> sub) override {
        Menu__push(sub, false);
        pushStringInternal(label);
        invokeMethod(menu_addSubmenu);
    }
    void addSeparator() override {
        invokeMethod(menu_addSeparator);
    }
};

inline void Menu__push(std::shared_ptr<Menu> inst, bool isReturn) {
    auto pushable = std::dynamic_pointer_cast<Pushable>(inst);
    pushable->push(pushable, isReturn);
}

std::shared_ptr<Menu> Menu__pop()
{
    bool isClientID, inSafetyArea;
    auto id = ni_popInstance(&isClientID, &inSafetyArea);
    if (isClientID) {
        return std::shared_ptr<Menu>(new ClientMenu(id));
    }
    else {
        return ServerMenu::getByID(id);
    }
}

class ClientMenuBar : public ClientObject, public MenuBar {
public:
    ClientMenuBar(int id) : ClientObject(id) {}
    void addMenu(std::string label, std::shared_ptr<Menu> menu) override {
        Menu__push(menu, false);
        pushStringInternal(label);
        invokeMethod(menuBar_addMenu);
    }
};

inline void MenuBar__push(std::shared_ptr<MenuBar> inst, bool isReturn) {
    auto pushable = std::dynamic_pointer_cast<Pushable>(inst);
    pushable->push(pushable, isReturn);
}

std::shared_ptr<MenuBar> MenuBar__pop()
{
    bool isClientID, inSafetyArea;
    auto id = ni_popInstance(&isClientID, &inSafetyArea);
    if (isClientID) {
        return std::shared_ptr<MenuBar>(new ClientMenuBar(id));
    }
    else {
        return ServerMenuBar::getByID(id);
    }
}

void MenuBar__push(std::shared_ptr<MenuBar> thing, bool isReturn);
std::shared_ptr<MenuBar> MenuBar__pop();

class ClientWindow : public ClientObject, public Window {
public:
    ClientWindow(int id) : ClientObject(id) {}
    void show() override {
        invokeMethod(window_show);
    }
    void setMenuBar(std::shared_ptr<MenuBar> mb) override {
        MenuBar__push(mb, false);
        invokeMethod(window_setMenuBar);
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
    Menu__push(createMenu(), true);
}

void createMenuBar__wrapper() {
    MenuBar__push(createMenuBar(), true);
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

void Window_setMenuBar__wrapper(int serverID) {
    auto inst = ServerWindow::getByID(serverID);
    auto mb = MenuBar__pop();
    inst->setMenuBar(mb);
}

void Menu_addAction__wrapper(int serverID) {
    auto inst = ServerMenu::getByID(serverID);
    auto action = (Action)ni_popSizeT();
    inst->addAction(action);
}

void Menu_addSubmenu__wrapper(int serverID) {
    auto inst = ServerMenu::getByID(serverID);
    auto label = popStringInternal();
    auto sub = Menu__pop();
    inst->addSubmenu(label, sub);
}

void Menu_addSeparator__wrapper(int serverID) {
    auto inst = ServerMenu::getByID(serverID);
    inst->addSeparator();
}

void MenuBar_addMenu__wrapper(int serverID) {
    auto inst = ServerMenuBar::getByID(serverID);
    auto label = popStringInternal();
    auto menu = Menu__pop();
    inst->addMenu(label, menu);
}

extern "C" int nativeLibraryInit() {
    auto m = ni_registerModule("TestLibrary");
    ni_registerModuleMethod(m, "createWindow", &createWindow__wrapper);
    ni_registerModuleMethod(m, "runloop", &runloop__wrapper);
    ni_registerModuleMethod(m, "exitRunloop", &exitRunloop__wrapper);
    ni_registerModuleMethod(m, "createAccelerator", &createAccelerator__wrapper);
    ni_registerModuleMethod(m, "createAction", &createAction__wrapper);
    ni_registerModuleMethod(m, "createMenu", &createMenu__wrapper);
    ni_registerModuleMethod(m, "createMenuBar", &createMenuBar__wrapper);
    ni_registerModuleMethod(m, "moduleInit", &moduleInit__wrapper);
    ni_registerModuleMethod(m, "moduleShutdown", &moduleShutdown__wrapper);
    auto windowDelegate = ni_registerInterface(m, "WindowDelegate");
    windowDelegate_onDestroyed = ni_registerInterfaceMethod(windowDelegate, "onDestroyed", &WindowDelegate_onDestroyed__wrapper);
    auto window = ni_registerInterface(m, "Window");
    window_show = ni_registerInterfaceMethod(window, "show", &Window_show__wrapper);
    window_setMenuBar = ni_registerInterfaceMethod(window, "setMenuBar", &Window_setMenuBar__wrapper);
    auto menu = ni_registerInterface(m, "Menu");
    menu_addAction = ni_registerInterfaceMethod(menu, "addAction", &Menu_addAction__wrapper);
    menu_addSubmenu = ni_registerInterfaceMethod(menu, "addSubmenu", &Menu_addSubmenu__wrapper);
    menu_addSeparator = ni_registerInterfaceMethod(menu, "addSeparator", &Menu_addSeparator__wrapper);
    auto menuBar = ni_registerInterface(m, "MenuBar");
    menuBar_addMenu = ni_registerInterfaceMethod(menuBar, "addMenu", &MenuBar_addMenu__wrapper);
    return 0; // = OK
}

extern "C" void nativeLibraryShutdown() {
}
