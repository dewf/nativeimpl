#include "NativeImplServer.h"
#include "Implementation.h"

ni_InterfaceMethodRef windowDelegate_onCloseRequest;
ni_InterfaceMethodRef windowDelegate_onDestroyed;
ni_InterfaceMethodRef windowDelegate_onResized;
ni_InterfaceMethodRef windowDelegate_onRepaint;
ni_InterfaceMethodRef windowDelegate_onAction;
ni_InterfaceMethodRef windowDelegate_onMouseEvent;




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



inline void Modifiers__push(int value) {
    ni_pushInt(value);
}

inline int Modifiers__pop() {
    return ni_popInt();
}

inline void MouseButton__push(MouseButton value) {
    ni_pushInt((int)value);
}

inline MouseButton MouseButton__pop() {
    auto tag = ni_popInt();
    return (MouseButton)tag;
}

inline void MouseEventType__push(MouseEventType value) {
    ni_pushInt((int)value);
}

inline MouseEventType MouseEventType__pop() {
    auto tag = ni_popInt();
    return (MouseEventType)tag;
}

inline void MouseWheelAxis__push(MouseWheelAxis value) {
    ni_pushInt((int)value);
}

inline MouseWheelAxis MouseWheelAxis__pop() {
    auto tag = ni_popInt();
    return (MouseWheelAxis)tag;
}

void MouseEvent__push(MouseEvent value, bool isReturn) {
    Modifiers__push(value.modifiers);
    MouseButton__push(value.button);
    MouseWheelAxis__push(value.wheelAxis);
    ni_pushInt(value.wheelDelta);
    ni_pushInt(value.y);
    ni_pushInt(value.x);
    MouseEventType__push(value.eventType);
}

MouseEvent MouseEvent__pop() {
    auto eventType = MouseEventType__pop();
    auto x = ni_popInt();
    auto y = ni_popInt();
    auto wheelDelta = ni_popInt();
    auto wheelAxis = MouseWheelAxis__pop();
    auto button = MouseButton__pop();
    auto modifiers = Modifiers__pop();
    return MouseEvent { eventType, x, y, wheelDelta, wheelAxis, button, modifiers };
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
        ni_pushPtr(context);
        invokeMethod(windowDelegate_onRepaint);
    }
    void onAction(int id, Action action) override {
        ni_pushPtr(action);
        ni_pushInt(id);
        invokeMethod(windowDelegate_onAction);
    }
    void onMouseEvent(MouseEvent mouseEvent) override {
        MouseEvent__push(mouseEvent, false);
        invokeMethod(windowDelegate_onMouseEvent);
    }
};

inline void WindowDelegate__push(std::shared_ptr<WindowDelegate> inst, bool isReturn) {
    if (inst != nullptr) {
        auto pushable = std::dynamic_pointer_cast<Pushable>(inst);
        pushable->push(pushable, isReturn);
    }
    else {
        ni_pushNullInstance();
    }
}

std::shared_ptr<WindowDelegate> WindowDelegate__pop()
{
    bool isClientID, inSafetyArea;
    auto id = ni_popInstance(&isClientID, &inSafetyArea);
    if (id != 0) {
        if (isClientID) {
            return std::shared_ptr<WindowDelegate>(new ClientWindowDelegate(id));
        }
        else {
            return ServerWindowDelegate::getByID(id);
        }
    }
    else {
        return std::shared_ptr<WindowDelegate>();
    }
}

void moduleInit__wrapper() {
    ni_pushInt(moduleInit());
}

void moduleShutdown__wrapper() {
    moduleShutdown();
}

void windowShow__wrapper() {
    auto _this = (Window)ni_popPtr();
    windowShow(_this);
}

void windowSetMenuBar__wrapper() {
    auto _this = (Window)ni_popPtr();
    auto mb = (MenuBar)ni_popPtr();
    windowSetMenuBar(_this, mb);
}

void windowInvalidate__wrapper() {
    auto _this = (Window)ni_popPtr();
    auto x = ni_popInt();
    auto y = ni_popInt();
    auto width = ni_popInt();
    auto height = ni_popInt();
    windowInvalidate(_this, x, y, width, height);
}

void windowDestroy__wrapper() {
    auto _this = (Window)ni_popPtr();
    windowDestroy(_this);
}

void createWindow__wrapper() {
    auto title = popStringInternal();
    auto del = WindowDelegate__pop();
    ni_pushPtr(createWindow(title, del));
}

void createAccelerator__wrapper() {
    auto key = Key__pop();
    auto modifiers = Modifiers__pop();
    ni_pushPtr(createAccelerator(key, modifiers));
}

void createAction__wrapper() {
    auto id = ni_popInt();
    auto label = popStringInternal();
    auto acc = (Accelerator)ni_popPtr();
    ni_pushPtr(createAction(id, label, acc));
}

void menuAddAction__wrapper() {
    auto _this = (Menu)ni_popPtr();
    auto action = (Action)ni_popPtr();
    menuAddAction(_this, action);
}

void menuAddSubmenu__wrapper() {
    auto _this = (Menu)ni_popPtr();
    auto label = popStringInternal();
    auto sub = (Menu)ni_popPtr();
    menuAddSubmenu(_this, label, sub);
}

void menuAddSeparator__wrapper() {
    auto _this = (Menu)ni_popPtr();
    menuAddSeparator(_this);
}

void createMenu__wrapper() {
    ni_pushPtr(createMenu());
}

void menuBarAddMenu__wrapper() {
    auto _this = (MenuBar)ni_popPtr();
    auto label = popStringInternal();
    auto menu = (Menu)ni_popPtr();
    menuBarAddMenu(_this, label, menu);
}

void createMenuBar__wrapper() {
    ni_pushPtr(createMenuBar());
}

void runloop__wrapper() {
    ni_pushInt(runloop());
}

void exitRunloop__wrapper() {
    exitRunloop();
}

void cGContextSetRGBFillColor__wrapper() {
    auto _this = (CGContext)ni_popPtr();
    auto red = ni_popDouble();
    auto green = ni_popDouble();
    auto blue = ni_popDouble();
    auto alpha = ni_popDouble();
    cGContextSetRGBFillColor(_this, red, green, blue, alpha);
}

void cGContextFillRect__wrapper() {
    auto _this = (CGContext)ni_popPtr();
    auto rect = CGRect__pop();
    cGContextFillRect(_this, rect);
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
    auto context = (CGContext)ni_popPtr();
    inst->onRepaint(context);
}

void WindowDelegate_onAction__wrapper(int serverID) {
    auto inst = ServerWindowDelegate::getByID(serverID);
    auto id = ni_popInt();
    auto action = (Action)ni_popPtr();
    inst->onAction(id, action);
}

void WindowDelegate_onMouseEvent__wrapper(int serverID) {
    auto inst = ServerWindowDelegate::getByID(serverID);
    auto mouseEvent = MouseEvent__pop();
    inst->onMouseEvent(mouseEvent);
}

extern "C" int nativeLibraryInit() {
    auto m = ni_registerModule("TestLibrary");
    ni_registerModuleMethod(m, "moduleInit", &moduleInit__wrapper);
    ni_registerModuleMethod(m, "moduleShutdown", &moduleShutdown__wrapper);
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
    ni_registerModuleMethod(m, "runloop", &runloop__wrapper);
    ni_registerModuleMethod(m, "exitRunloop", &exitRunloop__wrapper);
    ni_registerModuleMethod(m, "cGContextSetRGBFillColor", &cGContextSetRGBFillColor__wrapper);
    ni_registerModuleMethod(m, "cGContextFillRect", &cGContextFillRect__wrapper);
    auto windowDelegate = ni_registerInterface(m, "WindowDelegate");
    windowDelegate_onCloseRequest = ni_registerInterfaceMethod(windowDelegate, "onCloseRequest", &WindowDelegate_onCloseRequest__wrapper);
    windowDelegate_onDestroyed = ni_registerInterfaceMethod(windowDelegate, "onDestroyed", &WindowDelegate_onDestroyed__wrapper);
    windowDelegate_onResized = ni_registerInterfaceMethod(windowDelegate, "onResized", &WindowDelegate_onResized__wrapper);
    windowDelegate_onRepaint = ni_registerInterfaceMethod(windowDelegate, "onRepaint", &WindowDelegate_onRepaint__wrapper);
    windowDelegate_onAction = ni_registerInterfaceMethod(windowDelegate, "onAction", &WindowDelegate_onAction__wrapper);
    windowDelegate_onMouseEvent = ni_registerInterfaceMethod(windowDelegate, "onMouseEvent", &WindowDelegate_onMouseEvent__wrapper);
    return 0; // = OK
}

extern "C" void nativeLibraryShutdown() {
}
