#include "Implementation.h"
#include "../../../../moonshot/openwl/source/openwl.h"

struct __Accelerator {
	wl_AcceleratorRef wlAccel;
};
struct __Action {
	wl_ActionRef wlAction;
};

class PrivateMenu : public ServerMenu {
private:
	wl_MenuRef menu;
	friend class PrivateMenuBar;
public:
	PrivateMenu(wl_MenuRef menu) : menu(menu) {}
	void addAction(Action action) override {
		wl_MenuAddAction(menu, action->wlAction);
	}
	void addSubmenu(std::string label, std::shared_ptr<Menu> sub) override {
		auto other = std::dynamic_pointer_cast<PrivateMenu>(sub);
		wl_MenuAddSubmenu(menu, label.c_str(), other->menu);
	}
	void addSeparator() override {
		wl_MenuAddSeparator(menu);
	}
};

class PrivateMenuBar : public ServerMenuBar {
private:
	wl_MenuBarRef menuBar;
	friend class PrivateWindow;
public:
	PrivateMenuBar(wl_MenuBarRef menuBar) : menuBar(menuBar) {}
	void addMenu(std::string label, std::shared_ptr<Menu> menu) override {
		printf("in C++ PrivateMenuBar::addMenu\n");
		auto serverMenu = std::dynamic_pointer_cast<PrivateMenu>(menu);
		printf("menubar %08p adding menu %08p\n", menuBar, serverMenu->menu);
		wl_MenuBarAddMenu(menuBar, label.c_str(), serverMenu->menu);
	}
};

class PrivateWindow : public ServerWindow {
private:
	wl_WindowRef handle = nullptr;
	std::shared_ptr<WindowDelegate> del;
public:
	PrivateWindow(std::shared_ptr<WindowDelegate> del)
		: del(del) {}
	void setHandle(wl_WindowRef handle) {
		this->handle = handle;
	}
	// callback events
	void onDestroyed() {
		del->onDestroyed();
	}
	// interface methods ===================
	void show() override {
		wl_WindowShow(handle);
	}
	void setMenuBar(std::shared_ptr<MenuBar> mb) override {
		auto mb2 = std::dynamic_pointer_cast<PrivateMenuBar>(mb);
		wl_WindowSetMenuBar(handle, mb2->menuBar);
	}
};

int myCallback(wl_WindowRef window, struct wl_Event* event, void* userData) {
	event->handled = true;
	auto privWindow = (PrivateWindow*)userData;
	switch (event->eventType) {
	case wl_kEventTypeWindowDestroyed:
		privWindow->onDestroyed();
		break;
	default:
		event->handled = false;
	}
	return 0;
}

std::shared_ptr<Window> createWindow(std::string title, std::shared_ptr<WindowDelegate> del) {
	wl_WindowProperties props{};
	auto window = new PrivateWindow(del);
	auto handle = wl_WindowCreate(1024, 768, title.c_str(), window, &props);
	window->setHandle(handle);
	return std::shared_ptr<Window>(window);
}

Accelerator createAccelerator(KeyEnum key) {
	return new __Accelerator{ wl_AccelCreate((wl_KeyEnum)key, wl_kModifierControl | wl_kModifierShift) };
}

Action createAction(int id, std::string label, Accelerator acc) {
	return new __Action{ wl_ActionCreate(id, label.c_str(), nullptr, acc->wlAccel) };
}

std::shared_ptr<Menu> createMenu() {
	auto ret = wl_MenuCreate();
	return std::shared_ptr<Menu>(new PrivateMenu(ret));
}

std::shared_ptr<MenuBar> createMenuBar() {
	auto ret = wl_MenuBarCreate();
	return std::shared_ptr<MenuBar>(new PrivateMenuBar(ret));
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
