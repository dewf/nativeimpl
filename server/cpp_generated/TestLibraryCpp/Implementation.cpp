#include "Implementation.h"
#include "../../../../moonshot/openwl/source/openwl.h"
#include "../../../../moonshot/opendl/source/opendl.h"

#include <assert.h>

#define DLRECT(x) (*((dl_CGRect*)&x))

static int myCallback(wl_WindowRef window, struct wl_Event* event, void* userData);

struct __Window {
    wl_WindowRef wlWindow;
    std::shared_ptr<WindowDelegate> del;
};

struct __Accelerator {
    wl_AcceleratorRef wlAccel;
};

struct __Action {
    wl_ActionRef wlAction;
};

struct __Menu {
    wl_MenuRef wlMenu;
};

struct __MenuBar {
    wl_MenuBarRef wlMenuBar;
};

struct __CGContext {
    dl_CGContextRef dlContext;
};

Window createWindow(std::string title, std::shared_ptr<WindowDelegate> del) {
    wl_WindowProperties props{};
    auto ret = new __Window{ nullptr, del };
    ret->wlWindow = wl_WindowCreate(1024, 768, title.c_str(), ret, &props);
    return ret;
}

void windowShow(Window w) {
    wl_WindowShow(w->wlWindow);
}

void windowSetMenuBar(Window w, MenuBar mb) {
    wl_WindowSetMenuBar(w->wlWindow, mb->wlMenuBar);
}

void windowInvalidate(Window w, int x, int y, int width, int height)
{
    wl_WindowInvalidate(w->wlWindow, x, y, width, height);
}

void windowDestroy(Window w) {
    wl_WindowDestroy(w->wlWindow);
    delete w;
}

static unsigned int modifiersToBits(std::set<Modifier> modifiers) {
    unsigned int result = 0;
    for (auto i = modifiers.begin(); i != modifiers.end(); i++) {
        switch (*i) {
        case Modifier::Shift:
            result |= wl_kModifierShift;
            break;
        case Modifier::Control:
            result |= wl_kModifierControl;
            break;
        case Modifier::Alt:
            result |= wl_kModifierAlt;
            break;
        case Modifier::MacControl:
            result |= wl_kModifierMacControl;
            break;
        }
    }
    return result;
}

Accelerator createAccelerator(Key key, std::set<Modifier> modifiers) {
    auto ret = wl_AccelCreate((wl_KeyEnum)key, modifiersToBits(modifiers));
    return new __Accelerator{ ret };
}

Action createAction(int id, std::string label, Accelerator acc) {
    auto ret = wl_ActionCreate(id, label.c_str(), nullptr, acc->wlAccel);
    return new __Action{ ret };
}

Menu createMenu() {
    auto ret = wl_MenuCreate();
    return new __Menu{ ret };
}

void menuAddAction(Menu menu, Action action) {
    wl_MenuAddAction(menu->wlMenu, action->wlAction);
}

void menuAddSubmenu(Menu menu, std::string label, Menu sub) {
    wl_MenuAddSubmenu(menu->wlMenu, label.c_str(), sub->wlMenu);
}

void menuAddSeparator(Menu menu) {
    wl_MenuAddSeparator(menu->wlMenu);
}

MenuBar createMenuBar() {
    auto ret = wl_MenuBarCreate();
    return new __MenuBar{ ret };
}

void menuBarAddMenu(MenuBar mb, std::string label, Menu menu) {
    wl_MenuBarAddMenu(mb->wlMenuBar, label.c_str(), menu->wlMenu);
}

void cGContextSetRGBFillColor(CGContext c, double red, double green, double blue, double alpha)
{
    dl_CGContextSetRGBFillColor(c->dlContext, red, green, blue, alpha);
}

void cGContextFillRect(CGContext c, CGRect rect)
{
    dl_CGContextFillRect(c->dlContext, DLRECT(rect));
}

int runloop() {
    return wl_Runloop();
}

void exitRunloop() {
    wl_ExitRunloop();
}

int moduleInit() {
    wl_PlatformOptions wlOpts{};
    wlOpts.useDirect2D = true;
    if (wl_Init(&myCallback, &wlOpts) == 0) {
        dl_PlatformOptions dlOpts;
        dlOpts.factory = wlOpts.outParams.factory;
        return dl_Init(&dlOpts);
    }
    return -1;
}

void moduleShutdown() {
    dl_Shutdown();
    wl_Shutdown();
}

dl_CGContextRef createContext(wl_PlatformContext* platformContext) {
#ifdef WL_PLATFORM_WINDOWS
    auto context = dl_CGContextCreateD2D(platformContext->d2d.target);
#elif defined(WL_PLATFORM_MACOS)
    auto context = dl_CGContextCreateQuartz(platformContext->context, height);
#elif defined(WL_PLATFORM_LINUX)
    auto context = dl_CGContextCreateCairo(platformContext->cr, width, height);
#endif
    return context;
}

static int myCallback(wl_WindowRef window, struct wl_Event* event, void* userData) {
    auto privWindow = (Window)userData;
    event->handled = true;
    switch (event->eventType) {
    case wl_kEventTypeWindowCloseRequest: {
        auto reply = privWindow->del->onCloseRequest();
        event->closeRequestEvent.cancelClose = reply.cancelClose;
        break;
    }
    case wl_kEventTypeWindowDestroyed:
        privWindow->del->onDestroyed();
        break;
    case wl_kEventTypeWindowResized:
        privWindow->del->onResized(event->resizeEvent.newWidth, event->resizeEvent.newHeight);
        break;
    case wl_kEventTypeAction: {
        __Action action{ event->actionEvent.action };
        privWindow->del->onAction(event->actionEvent.id, &action);
        break;
    }
    case wl_kEventTypeWindowRepaint: {
        auto dlContext = createContext(&event->repaintEvent.platformContext);
        dl_CGContextSaveGState(dlContext);

        __CGContext context{ dlContext };
        privWindow->del->onRepaint(&context);

        dl_CGContextRestoreGState(dlContext);
        dl_CGContextRelease(dlContext);
        break;
    }
#ifdef WL_PLATFORM_WINDOWS
    case wl_kEventTypeD2DTargetRecreated:
        // this just lets us know to flush any cached stuff we have for a target / init a new cache
        dl_D2DTargetRecreated(event->d2dTargetRecreatedEvent.newTarget, event->d2dTargetRecreatedEvent.oldTarget);
        break;
#endif
    default:
        event->handled = false;
    }
    return 0;
}
