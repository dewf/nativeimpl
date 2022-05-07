#include "Implementation.h"
#include "../../../../moonshot/openwl/source/openwl.h"

int myCallback(wl_WindowRef window, struct wl_Event* event, void* userData);

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

void windowDestroy(Window w) {
    wl_WindowDestroy(w->wlWindow);
}

Accelerator createAccelerator(KeyEnum key) {
    auto ret = wl_AccelCreate((wl_KeyEnum)key, wl_kModifierControl | wl_kModifierShift);
    return new __Accelerator{ ret };
}

Action createAction(int id, std::string label, Accelerator acc) {
    auto ret = wl_ActionCreate(id, label.c_str(), nullptr, acc->wlAccel);
    return new __Action{ ret };
}

Menu createMenu() {
    auto ret = wl_MenuCreate();
    return new __Menu { ret };
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

int runloop() {
    return wl_Runloop();
}

void exitRunloop() {
    wl_ExitRunloop();
}

int moduleInit() {
    wl_PlatformOptions opts{};
    opts.useDirect2D = false;
    return wl_Init(&myCallback, &opts);
}

void moduleShutdown() {
    wl_Shutdown();
}

int myCallback(wl_WindowRef window, struct wl_Event* event, void* userData) {
    event->handled = true;
    auto privWindow = (Window)userData;
    switch (event->eventType) {
    case wl_kEventTypeWindowDestroyed:
        privWindow->del->onDestroyed();
        break;
    default:
        event->handled = false;
    }
    return 0;
}
