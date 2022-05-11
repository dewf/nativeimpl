#include "NativeImplServer.h"
#include <functional>
#include <memory>
#include <string>
#include <vector>
#include <map>
#include <tuple>
#include <set>

struct __Accelerator; typedef struct __Accelerator* Accelerator;

struct __Action; typedef struct __Action* Action;

struct __CGContext; typedef struct __CGContext* CGContext;

struct CGPoint {
    double x;
    double y;
};

struct CGSize {
    double width;
    double height;
};

struct CGRect {
    CGPoint origin;
    CGSize size;
};

struct CloseRequestReply {
    bool cancelClose;
};

enum class Key {
    Unknown,
    Escape,
    Tab,
    Backspace,
    Return,
    Space,
    F1,
    F2,
    F3,
    F4,
    F5,
    F6,
    F7,
    F8,
    F9,
    F10,
    F11,
    F12,
    F13,
    F14,
    F15,
    F16,
    F17,
    F18,
    F19,
    _0,
    _1,
    _2,
    _3,
    _4,
    _5,
    _6,
    _7,
    _8,
    _9,
    A,
    B,
    C,
    D,
    E,
    F,
    G,
    H,
    I,
    J,
    K,
    L,
    M,
    N,
    O,
    P,
    Q,
    R,
    S,
    T,
    U,
    V,
    W,
    X,
    Y,
    Z,
    Control,
    Shift,
    AltOption,
    WinCommand,
    Fn,
    Insert,
    Delete,
    PageUp,
    PageDown,
    Home,
    End,
    LeftArrow,
    UpArrow,
    RightArrow,
    DownArrow,
    KP0,
    KP1,
    KP2,
    KP3,
    KP4,
    KP5,
    KP6,
    KP7,
    KP8,
    KP9,
    KPClear,
    KPEquals,
    KPDivide,
    KPMultiply,
    KPSubtract,
    KPAdd,
    KPEnter,
    KPDecimal,
    CapsLock,
    NumLock,
    ScrollLock,
    PrintScreen,
    Pause,
    Cancel,
    MediaMute,
    MediaVolumeDown,
    MediaVolumeUp,
    MediaNext,
    MediaPrev,
    MediaStop,
    MediaPlayPause
};

struct __Menu; typedef struct __Menu* Menu;

struct __MenuBar; typedef struct __MenuBar* MenuBar;

enum class Modifier {
    Shift,
    Control,
    Alt,
    MacControl
};

struct __Window; typedef struct __Window* Window;

class WindowDelegate {
public:
    virtual CloseRequestReply onCloseRequest() = 0;
    virtual void onDestroyed() = 0;
    virtual void onResized(int newWidth, int newHeight) = 0;
    virtual void onRepaint(CGContext context) = 0;
    virtual void onAction(int id, Action action) = 0;
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

// std::set<Modifier>

void windowShow(Window _this);
void windowSetMenuBar(Window _this, MenuBar mb);
void windowInvalidate(Window _this, int x, int y, int width, int height);
void windowDestroy(Window _this);
Window createWindow(std::string title, std::shared_ptr<WindowDelegate> del);
Accelerator createAccelerator(Key key, std::set<Modifier> modifiers);
Action createAction(int id, std::string label, Accelerator acc);
void menuAddAction(Menu _this, Action action);
void menuAddSubmenu(Menu _this, std::string label, Menu sub);
void menuAddSeparator(Menu _this);
Menu createMenu();
void menuBarAddMenu(MenuBar _this, std::string label, Menu menu);
MenuBar createMenuBar();
void cGContextSetRGBFillColor(CGContext _this, double red, double green, double blue, double alpha);
void cGContextFillRect(CGContext _this, CGRect rect);
int runloop();
void exitRunloop();
int moduleInit();
void moduleShutdown();
