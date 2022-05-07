#include "NativeImplServer.h"
#include <functional>
#include <memory>
#include <string>
#include <vector>
#include <map>
#include <tuple>

struct __Accelerator; typedef struct __Accelerator* Accelerator;

struct __Action; typedef struct __Action* Action;

enum class KeyEnum {
    KeyUnknown,
    KeyEscape,
    KeyTab,
    KeyBackspace,
    KeyReturn,
    KeySpace,
    KeyF1,
    KeyF2,
    KeyF3,
    KeyF4,
    KeyF5,
    KeyF6,
    KeyF7,
    KeyF8,
    KeyF9,
    KeyF10,
    KeyF11,
    KeyF12,
    KeyF13,
    KeyF14,
    KeyF15,
    KeyF16,
    KeyF17,
    KeyF18,
    KeyF19,
    Key0,
    Key1,
    Key2,
    Key3,
    Key4,
    Key5,
    Key6,
    Key7,
    Key8,
    Key9,
    KeyA,
    KeyB,
    KeyC,
    KeyD,
    KeyE,
    KeyF,
    KeyG,
    KeyH,
    KeyI,
    KeyJ,
    KeyK,
    KeyL,
    KeyM,
    KeyN,
    KeyO,
    KeyP,
    KeyQ,
    KeyR,
    KeyS,
    KeyT,
    KeyU,
    KeyV,
    KeyW,
    KeyX,
    KeyY,
    KeyZ,
    KeyControl,
    KeyShift,
    KeyAltOption,
    KeyWinCommand,
    KeyFn,
    KeyInsert,
    KeyDelete,
    KeyPageUp,
    KeyPageDown,
    KeyHome,
    KeyEnd,
    KeyLeftArrow,
    KeyUpArrow,
    KeyRightArrow,
    KeyDownArrow,
    KeyKP0,
    KeyKP1,
    KeyKP2,
    KeyKP3,
    KeyKP4,
    KeyKP5,
    KeyKP6,
    KeyKP7,
    KeyKP8,
    KeyKP9,
    KeyKPClear,
    KeyKPEquals,
    KeyKPDivide,
    KeyKPMultiply,
    KeyKPSubtract,
    KeyKPAdd,
    KeyKPEnter,
    KeyKPDecimal,
    KeyCapsLock,
    KeyNumLock,
    KeyScrollLock,
    KeyPrintScreen,
    KeyPause,
    KeyCancel,
    KeyMediaMute,
    KeyMediaVolumeDown,
    KeyMediaVolumeUp,
    KeyMediaNext,
    KeyMediaPrev,
    KeyMediaStop,
    KeyMediaPlayPause
};

class Menu; // fwd decl

class Menu {
public:
    virtual void addAction(Action action) = 0;
    virtual void addSubmenu(std::string label, std::shared_ptr<Menu> sub) = 0;
    virtual void addSeparator() = 0;
};

// inherit from this to create server instances of Menu
class ServerMenu : public Menu, public ServerObject {
public:
    virtual ~ServerMenu() {}
    static std::shared_ptr<ServerMenu> getByID(int id) {
        auto obj = ServerObject::getByID(id);
        return std::static_pointer_cast<ServerMenu>(obj);
    }
};

class MenuBar {
public:
    virtual void addMenu(std::string label, std::shared_ptr<Menu> menu) = 0;
};

// inherit from this to create server instances of MenuBar
class ServerMenuBar : public MenuBar, public ServerObject {
public:
    virtual ~ServerMenuBar() {}
    static std::shared_ptr<ServerMenuBar> getByID(int id) {
        auto obj = ServerObject::getByID(id);
        return std::static_pointer_cast<ServerMenuBar>(obj);
    }
};

class MenuBar; // fwd decl

class Window {
public:
    virtual void show() = 0;
    virtual void setMenuBar(std::shared_ptr<MenuBar> mb) = 0;
};

// inherit from this to create server instances of Window
class ServerWindow : public Window, public ServerObject {
public:
    virtual ~ServerWindow() {}
    static std::shared_ptr<ServerWindow> getByID(int id) {
        auto obj = ServerObject::getByID(id);
        return std::static_pointer_cast<ServerWindow>(obj);
    }
};

class WindowDelegate {
public:
    virtual void onDestroyed() = 0;
};

// inherit from this to create server instances of WindowDelegate
class ServerWindowDelegate : public WindowDelegate, public ServerObject {
public:
    virtual ~ServerWindowDelegate() {}
    static std::shared_ptr<ServerWindowDelegate> getByID(int id) {
        auto obj = ServerObject::getByID(id);
        return std::static_pointer_cast<ServerWindowDelegate>(obj);
    }
};

std::shared_ptr<Window> createWindow(std::string title, std::shared_ptr<WindowDelegate> del);
int runloop();
void exitRunloop();
Accelerator createAccelerator(KeyEnum key);
Action createAction(int id, std::string label, Accelerator acc);
std::shared_ptr<Menu> createMenu();
std::shared_ptr<MenuBar> createMenuBar();
int moduleInit();
void moduleShutdown();
