import testlibrary_native as nativemethods


class NativeHandle:
    def __init__(self, handle):
        self.handle = handle


class ModuleHandle(NativeHandle):
    def __init__(self, handle):
        super().__init__(handle)


_exception_builders = {}  # e.handle -> build_and_throw()


def set_exception_builder(eh, builder):
    _exception_builders[eh.handle] = builder


class MethodHandle(NativeHandle):
    def __init__(self, handle):
        super().__init__(handle)

    def invoke(self):
        nativemethods.invoke_module_method(self.handle)

    def invoke_with_exceptions(self):
        e_handle = nativemethods.invoke_module_method_with_exceptions(self.handle)
        if e_handle:
            build_and_throw = _exception_builders[e_handle]
            build_and_throw()


class InterfaceHandle(NativeHandle):
    def __init__(self, handle):
        super().__init__(handle)


class InterfaceMethodHandle(NativeHandle):
    def __init__(self, handle):
        super().__init__(handle)

    def invoke(self, obj_id):
        nativemethods.invoke_interface_method(self.handle, obj_id)

    def invoke_with_exceptions(self, obj_id):
        e_handle = nativemethods.invoke_interface_method_with_exceptions(self.handle, obj_id)
        if e_handle:
            build_and_throw = _exception_builders[e_handle]
            build_and_throw()


class ExceptionHandle(NativeHandle):
    def __init__(self, handle):
        super().__init__(handle)

    def set(self):
        nativemethods.set_exception(self.handle)


def get_module(name):
    handle = nativemethods.get_module(name)
    return ModuleHandle(handle)


def get_module_method(module, name):
    handle = nativemethods.get_module_method(module.handle, name)
    return MethodHandle(handle)


def get_interface(module, name):
    handle = nativemethods.get_interface(module.handle, name)
    return InterfaceHandle(handle)


def get_interface_method(iface, name):
    handle = nativemethods.get_interface_method(iface.handle, name)
    return InterfaceMethodHandle(handle)


def get_exception(module, name):
    handle = nativemethods.get_exception(module.handle, name)
    return ExceptionHandle(handle)


def push_module_constants(module):
    nativemethods.push_module_constants(module.handle)


def invoke_module_method(method):
    nativemethods.invoke_module_method(method.handle)


def push_bool(value):
    nativemethods.push_bool(value)


def pop_bool():
    return nativemethods.pop_bool()


def push_int(value):
    nativemethods.push_int(value)


def pop_int():
    return nativemethods.pop_int()


class FuncItem:
    def __init__(self, fid, wrapper, raw_func):
        self.fid = fid
        self.wrapper = wrapper
        self.raw_func = raw_func
        self.ref_count = 1

    def add_ref(self):
        self.ref_count += 1

    def release(self):
        print(f"releasing FuncItem {self.fid} (refcount {self.ref_count})")
        self.ref_count -= 1
        if self.ref_count == 0:
            print(f"FuncItem {self.fid} - 0 refs, removing")
            del _func_table[self.fid]
            del _reverse_func_table[self.raw_func]


_next_func_id = 1
_func_table = {}  # id -> func item
_reverse_func_table = {}  # raw_func -> func item


def push_func(wrapper, raw_func):
    item = FuncItem
    if raw_func in _reverse_func_table:
        item = _reverse_func_table[raw_func]
        print(f"function {item.fid} already in table (refcount {item.ref_count})")
        item.add_ref()
    else:
        global _next_func_id
        item = FuncItem(_next_func_id, wrapper, raw_func)
        print(f"function f not yet in table: new id {item.fid}")
        _next_func_id += 1
        _func_table[item.fid] = item
        _reverse_func_table[raw_func] = item
    # end if
    nativemethods.push_client_func(item.fid)


class ServerFunc:
    def __init__(self, fid):
        self.fid = fid

    def invoke(self):
        nativemethods.exec_server_func(self.fid)

    def __del__(self):
        nativemethods.release_server_func(self.fid)


def pop_func():
    fid = nativemethods.pop_server_func()
    return ServerFunc(fid)


_safe_area = []  # safe ServerObjects - keep them until other side completes popping


class ServerObject:
    def __init__(self, obj_id):
        self.obj_id = obj_id
        self.remote_released = False

    def remote_release(self):
        if self.remote_released:
            return
        nativemethods.release_server_inst(self.obj_id)
        self.remote_released = True
        print(f"$$$ python - server obj {self.obj_id} remote released!")

    def server_push(self, is_return):
        nativemethods.push_server_inst(self.obj_id)
        if is_return:
            _safe_area.append(self)

    def __del__(self):
        print(f"python deleting server obj {self.obj_id}")
        self.remote_release()

    def __enter__(self):
        return self

    def __exit__(self, exc_type, exc_val, exc_tb):
        self.remote_release()


def pop_instance():
    return nativemethods.pop_instance()  # returns tuple if (id, is_client_id)


_next_client_obj_id = 1
_client_objects = {}  # id -> obj ... protect them from GC collection if 'checked out'


def _next_client_id():
    global _next_client_obj_id
    ret = _next_client_obj_id
    _next_client_obj_id += 1
    return ret


class ClientObject:
    def __init__(self):
        self.obj_id = -1  # not yet in table
        self.ref_count = 0

    def client_push(self):
        if self.obj_id == -1:
            # not yet in client object table
            self.obj_id = _next_client_id()
            _client_objects[self.obj_id] = self
        # end if
        self.ref_count += 1
        print(f"!!!ClientObject: push_client_inst_id {self.obj_id}")
        nativemethods.push_client_inst(self.obj_id)

    def release(self):
        print(f"releasing client obj {self.obj_id}")
        self.ref_count -= 1
        if self.ref_count == 0:
            # remove from table
            print(" - refcount 0, removing from table")
            del _client_objects[self.obj_id]
            self.obj_id = -1

    def __enter__(self):
        return self

    def __exit__(self, exc_type, exc_val, exc_tb):
        pass

    @staticmethod
    def get_by_id(obj_id):
        return _client_objects[obj_id]


_client_method_wrappers = {}  # method handle -> wrapper (takes client obj as parameter)


def set_client_method_wrapper(method, wrapper):
    _client_method_wrappers[method.handle] = wrapper


def clear_server_safety_area():
    nativemethods.clear_server_safety_area()


# private stuff =====================================
def client_func_exec(func_id):
    print(f"Python clientFuncExec fid:{func_id}")
    item = _func_table[func_id]
    # execute the wrapper
    item.wrapper()


def client_func_release(func_id):
    print(f"Python clientFuncRelease fid:{func_id}")
    item = _func_table[func_id]
    item.release()


def client_method_exec(native_handle, obj_id):
    print(f"@@@@Python clientMethodExec: {native_handle} - objid:{obj_id}")
    wrapper = _client_method_wrappers[native_handle]
    obj = _client_objects[obj_id]
    wrapper(obj)


def client_object_release(obj_id):
    print(f"== Python clientObjectRelease: objid:{obj_id}")
    _client_objects[obj_id].release()


def client_clear_safety_area():
    print("Python clientClearSafetyArea")
    _safe_area.clear()


def dump_tables():
    # server tables
    nativemethods.dump_tables();
    # our tables
    print("== client function table ==")
    for k in _func_table.keys():
        print(f" - {k}")
    print("== client object table ==")
    for k in _client_objects:
        print(f" - {k}")


def shutdown():
    nativemethods.shutdown()


# module init ========================================
nativemethods.init(client_func_exec, client_func_release, client_method_exec, client_object_release,
                   client_clear_safety_area)
