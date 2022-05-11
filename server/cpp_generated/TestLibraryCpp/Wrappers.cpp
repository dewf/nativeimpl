#include "NativeImplServer.h"
#include "Implementation.h"

ni_InterfaceMethodRef windowDelegate_onCloseRequest;
ni_InterfaceMethodRef windowDelegate_onDestroyed;
ni_InterfaceMethodRef windowDelegate_onResized;
ni_InterfaceMethodRef windowDelegate_onRepaint;
ni_InterfaceMethodRef windowDelegate_onAction;




void CGPoint__push(CGPoint value, bool isReturn) {
    ni_pushDouble(value.y);
    ni_pushDouble(value.x);
}

CGPoint CGPoint__pop() {
    auto x = ni_popDouble();
    auto y = ni_popDouble();
    return CGPoint { x, y };
}

void CGSize__push(CGSize value, bool isReturn) {
    ni_pushDouble(value.height);
    ni_pushDouble(value.width);
}

CGSize CGSize__pop() {
    auto width = ni_popDouble();
    auto height = ni_popDouble();
    return CGSize { width, height };
}

void CGRect__push(CGRect value, bool isReturn) {
    CGSize__push(value.size, isReturn);
    CGPoint__push(value.origin, isReturn);
}

CGRect CGRect__pop() {
    auto origin = CGPoint__pop();
    auto size = CGSize__pop();
    return CGRect { origin, size };
}

void CloseRequestReply__push(CloseRequestReply value, bool isReturn) {
    ni_pushBool(value.cancelClose);
}

CloseRequestReply CloseRequestReply__pop() {
    auto cancelClose = ni_popBool();
    return CloseRequestReply { cancelClose };
}

inline void Key__push(Key value) {
    ni_pushInt((int)value);
}

inline Key Key__pop() {
    auto tag = ni_popInt();
    return (Key)tag;
}



inline void Modifier__push(Modifier value) {
    ni_pushInt((int)value);
}

inline Modifier Modifier__pop() {
    auto tag = ni_popInt();
    return (Modifier)tag;
}


class ClientWindowDelegate : public ClientObject, public WindowDelegate {
public:
    ClientWindowDelegate(int id) : ClientObject(id) {}
    CloseRequestReply onCloseRequest() override {
        invokeMethod(windowDelegate_onCloseRequest);
        return CloseRequestReply__pop();
    }
    void onDestroyed() override {
        invokeMethod(windowDelegate_onDestroyed);
    }
    void onResized(int newWidth, int newHeight) override {
        ni_pushInt(newHeight);
        ni_pushInt(newWidth);
        invokeMethod(windowDelegate_onResized);
    }
    void onRepaint(CGContext context) override {
        ni_pushSizeT((size_t)context);
        invokeMethod(windowDelegate_onRepaint);
    }
    void onAction(int id, Action action) override {
        ni_pushSizeT((size_t)action);
        ni_pushInt(id);
        invokeMethod(windowDelegate_onAction);
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

void __ModifierSet__push(std::set<Modifier> values, bool isReturn) {
    std::vector<int> intValues;
    for (auto i = values.begin(); i != values.end(); i++) {
        intValues.push_back((int)*i);
    }
    pushIntArrayInternal(intValues);
}

std::set<Modifier> __ModifierSet__pop() {
    auto intValues = popIntArrayInternal();
    std::set<Modifier> __ret;
    for (auto i = intValues.begin(); i != intValues.end(); i++) {
        __ret.insert((Modifier)*i);
    }
    return __ret;
}

void windowShow__wrapper() {
    auto _this = (Window)ni_popSizeT();
    windowShow(_this);
}

void windowSetMenuBar__wrapper() {
    auto _this = (Window)ni_popSizeT();
    auto mb = (MenuBar)ni_popSizeT();
    windowSetMenuBar(_this, mb);
}

void windowInvalidate__wrapper() {
    auto _this = (Window)ni_popSizeT();
    auto x = ni_popInt();
    auto y = ni_popInt();
    auto width = ni_popInt();
    auto height = ni_popInt();
    windowInvalidate(_this, x, y, width, height);
}

void windowDestroy__wrapper() {
    auto _this = (Window)ni_popSizeT();
    windowDestroy(_this);
}

void createWindow__wrapper() {
    auto title = popStringInternal();
    auto del = WindowDelegate__pop();
    ni_pushSizeT((size_t)createWindow(title, del));
}

void createAccelerator__wrapper() {
    auto key = Key__pop();
    auto modifiers = __ModifierSet__pop();
    ni_pushSizeT((size_t)createAccelerator(key, modifiers));
}

void createAction__wrapper() {
    auto id = ni_popInt();
    auto label = popStringInternal();
    auto acc = (Accelerator)ni_popSizeT();
    ni_pushSizeT((size_t)createAction(id, label, acc));
}

void menuAddAction__wrapper() {
    auto _this = (Menu)ni_popSizeT();
    auto action = (Action)ni_popSizeT();
    menuAddAction(_this, action);
}

void menuAddSubmenu__wrapper() {
    auto _this = (Menu)ni_popSizeT();
    auto label = popStringInternal();
    auto sub = (Menu)ni_popSizeT();
    menuAddSubmenu(_this, label, sub);
}

void menuAddSeparator__wrapper() {
    auto _this = (Menu)ni_popSizeT();
    menuAddSeparator(_this);
}

void createMenu__wrapper() {
    ni_pushSizeT((size_t)createMenu());
}

void menuBarAddMenu__wrapper() {
    auto _this = (MenuBar)ni_popSizeT();
    auto label = popStringInternal();
    auto menu = (Menu)ni_popSizeT();
    menuBarAddMenu(_this, label, menu);
}

void createMenuBar__wrapper() {
    ni_pushSizeT((size_t)createMenuBar());
}

void cGContextSetRGBFillColor__wrapper() {
    auto _this = (CGContext)ni_popSizeT();
    auto red = ni_popDouble();
    auto green = ni_popDouble();
    auto blue = ni_popDouble();
    auto alpha = ni_popDouble();
    cGContextSetRGBFillColor(_this, red, green, blue, alpha);
}

void cGContextFillRect__wrapper() {
    auto _this = (CGContext)ni_popSizeT();
    auto rect = CGRect__pop();
    cGContextFillRect(_this, rect);
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

void WindowDelegate_onCloseRequest__wrapper(int serverID) {
    auto inst = ServerWindowDelegate::getByID(serverID);
    CloseRequestReply__push(inst->onCloseRequest(), true);
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

void WindowDelegate_onRepaint__wrapper(int serverID) {
    auto inst = ServerWindowDelegate::getByID(serverID);
    auto context = (CGContext)ni_popSizeT();
    inst->onRepaint(context);
}

void WindowDelegate_onAction__wrapper(int serverID) {
    auto inst = ServerWindowDelegate::getByID(serverID);
    auto id = ni_popInt();
    auto action = (Action)ni_popSizeT();
    inst->onAction(id, action);
}

extern "C" int nativeLibraryInit() {
    auto m = ni_registerModule("TestLibrary");
    ni_registerModuleMethod(m, "windowShow", &windowShow__wrapper);
    ni_registerModuleMethod(m, "windowSetMenuBar", &windowSetMenuBar__wrapper);
    ni_registerModuleMethod(m, "windowInvalidate", &windowInvalidate__wrapper);
    ni_registerModuleMethod(m, "windowDestroy", &windowDestroy__wrapper);
    ni_registerModuleMethod(m, "createWindow", &createWindow__wrapper);
    ni_registerModuleMethod(m, "createAccelerator", &createAccelerator__wrapper);
    ni_registerModuleMethod(m, "createAction", &createAction__wrapper);
    ni_registerModuleMethod(m, "menuAddAction", &menuAddAction__wrapper);
    ni_registerModuleMethod(m, "menuAddSubmenu", &menuAddSubmenu__wrapper);
    ni_registerModuleMethod(m, "menuAddSeparator", &menuAddSeparator__wrapper);
    ni_registerModuleMethod(m, "createMenu", &createMenu__wrapper);
    ni_registerModuleMethod(m, "menuBarAddMenu", &menuBarAddMenu__wrapper);
    ni_registerModuleMethod(m, "createMenuBar", &createMenuBar__wrapper);
    ni_registerModuleMethod(m, "cGContextSetRGBFillColor", &cGContextSetRGBFillColor__wrapper);
    ni_registerModuleMethod(m, "cGContextFillRect", &cGContextFillRect__wrapper);
    ni_registerModuleMethod(m, "runloop", &runloop__wrapper);
    ni_registerModuleMethod(m, "exitRunloop", &exitRunloop__wrapper);
    ni_registerModuleMethod(m, "moduleInit", &moduleInit__wrapper);
    ni_registerModuleMethod(m, "moduleShutdown", &moduleShutdown__wrapper);
    auto windowDelegate = ni_registerInterface(m, "WindowDelegate");
    windowDelegate_onCloseRequest = ni_registerInterfaceMethod(windowDelegate, "onCloseRequest", &WindowDelegate_onCloseRequest__wrapper);
    windowDelegate_onDestroyed = ni_registerInterfaceMethod(windowDelegate, "onDestroyed", &WindowDelegate_onDestroyed__wrapper);
    windowDelegate_onResized = ni_registerInterfaceMethod(windowDelegate, "onResized", &WindowDelegate_onResized__wrapper);
    windowDelegate_onRepaint = ni_registerInterfaceMethod(windowDelegate, "onRepaint", &WindowDelegate_onRepaint__wrapper);
    windowDelegate_onAction = ni_registerInterfaceMethod(windowDelegate, "onAction", &WindowDelegate_onAction__wrapper);
    return 0; // = OK
}

extern "C" void nativeLibraryShutdown() {
}
