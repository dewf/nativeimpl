#include "NativeImplServer.h"
#include <functional>
#include <memory>
#include <string>
#include <vector>
#include <map>
#include <tuple>

class Window {
public:
    virtual void show() = 0;
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
int moduleInit();
void moduleShutdown();
