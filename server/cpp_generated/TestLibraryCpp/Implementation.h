#include "NativeImplServer.h"
#include <functional>
#include <memory>
#include <string>
#include <vector>
#include <map>
#include <tuple>

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

struct __Menu; typedef struct __Menu* Menu;

struct __MenuBar; typedef struct __MenuBar* MenuBar;

struct __Window; typedef struct __Window* Window;

class WindowDelegate {
public:
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

Window createWindow(std::string title, std::shared_ptr<WindowDelegate> del);
void windowShow(Window w);
void windowSetMenuBar(Window w, MenuBar mb);
void windowInvalidate(Window w, int x, int y, int width, int height);
void windowDestroy(Window w);
Accelerator createAccelerator(KeyEnum key);
Action createAction(int id, std::string label, Accelerator acc);
Menu createMenu();
void menuAddAction(Menu menu, Action action);
void menuAddSubmenu(Menu menu, std::string label, Menu sub);
void menuAddSeparator(Menu menu);
MenuBar createMenuBar();
void menuBarAddMenu(MenuBar mb, std::string label, Menu menu);
void cgContextSetRGBFillColor(CGContext c, double red, double green, double blue, double alpha);
void cgContextFillRect(CGContext c, CGRect rect);
int runloop();
void exitRunloop();
int moduleInit();
void moduleShutdown();
