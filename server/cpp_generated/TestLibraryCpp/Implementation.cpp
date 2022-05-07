#include "Implementation.h"
#include "../../../../moonshot/openwl/source/openwl.h"

class PrivateWindow : public ServerWindow {
private:
	wl_WindowRef handle;
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
};

static int myCallback(wl_WindowRef window, struct wl_Event* event, void* userData) {
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
