import nativeimpl
import atexit

m = nativeimpl.get_module("TestLibrary")

# module constants
nativeimpl.push_module_constants(m)
global01 = nativeimpl.pop_int()
another_global = nativeimpl.pop_int()
last_int_global = nativeimpl.pop_int()

# module methods
_sum = nativeimpl.get_module_method(m, "sum")
_exec_func = nativeimpl.get_module_method(m, "execFunc01")
_stash_func = nativeimpl.get_module_method(m, "stashFunc")
_release_func = nativeimpl.get_module_method(m, "releaseFunc")
_return_stashed_func = nativeimpl.get_module_method(m, "returnStashedFunc")
_make_thing = nativeimpl.get_module_method(m, "makeThing")
_operate_on_thing = nativeimpl.get_module_method(m, "operateOnThing")
_save_thing = nativeimpl.get_module_method(m, "saveThing")
_get_saved_thing = nativeimpl.get_module_method(m, "getSavedThing")
_client_stash_test = nativeimpl.get_module_method(m, "clientStashTest")
_test_negative = nativeimpl.get_module_method(m, "testNegative")
_test_client_throw = nativeimpl.get_module_method(m, "testClientThrow")
_start_timer = nativeimpl.get_module_method(m, "startTimer")
_stop_timer = nativeimpl.get_module_method(m, "stopTimer")

# ithing01 stuff
_ithing = nativeimpl.get_interface(m, "IThing01")
_ithing_some_method = nativeimpl.get_interface_method(_ithing, "someMethod")
_ithing_stash_thing = nativeimpl.get_interface_method(_ithing, "stashThing")
_ithing_get_stashed_thing = nativeimpl.get_interface_method(_ithing, "getStashedThing")
_ithing_test_throw = nativeimpl.get_interface_method(_ithing, "testThrow")
_ithing_on_timer_tick = nativeimpl.get_interface_method(_ithing, "onTimerTick")


# client method wrappers
def _some_method_wrapper(obj):
    arg = nativeimpl.pop_int()
    res = obj.some_method(arg)
    nativeimpl.push_int(res)

def _stash_thing_wrapper(obj):
    thing = IThing01.pop()
    obj.stash_thing(thing)

def _get_stashed_thing_wrapper(obj):
    thing = obj.get_stashed_thing()
    thing.push(True)

def _test_throw_wrapper(obj):
    arg = nativeimpl.pop_bool()
    try:
        obj.test_throw(arg)
    except InterfaceMethodException as e:
        e.push_and_set()

def _on_timer_tick_wrapper(obj):
    obj.on_timer_tick()


nativeimpl.set_client_method_wrapper(_ithing_some_method, _some_method_wrapper)
nativeimpl.set_client_method_wrapper(_ithing_stash_thing, _stash_thing_wrapper)
nativeimpl.set_client_method_wrapper(_ithing_get_stashed_thing, _get_stashed_thing_wrapper)
nativeimpl.set_client_method_wrapper(_ithing_test_throw, _test_throw_wrapper)
nativeimpl.set_client_method_wrapper(_ithing_on_timer_tick, _on_timer_tick_wrapper)


# exceptions
class NegativeException(BaseException):
    def __init__(self, value) -> None:
        super(NegativeException, self).__init__()
        self.value = value

    def push_and_set(self):
        nativeimpl.push_int(self.value)
        _negative_exception.set()

    @staticmethod
    def pop_and_throw():
        value = nativeimpl.pop_int()
        raise NegativeException(value)


class InterfaceMethodException(BaseException):
    def __init__(self, value):
        super(InterfaceMethodException, self).__init__()
        self.value = value

    def push_and_set(self):
        nativeimpl.push_int(self.value)
        _interface_method_exception.set()

    @staticmethod
    def pop_and_throw():
        value = nativeimpl.pop_int()
        raise InterfaceMethodException(value)


_negative_exception = nativeimpl.get_exception(m, "NegativeException")
nativeimpl.set_exception_builder(_negative_exception, NegativeException.pop_and_throw)

_interface_method_exception = nativeimpl.get_exception(m, "InterfaceMethodException")
nativeimpl.set_exception_builder(_interface_method_exception, InterfaceMethodException.pop_and_throw)


def sum(a, b, c):
    nativeimpl.push_int(c)
    nativeimpl.push_int(b)
    nativeimpl.push_int(a)
    _sum.invoke()
    return nativeimpl.pop_int()


def exec_func(f):  # f: int -> fint
    def wrapper():
        arg = nativeimpl.pop_int()
        res = f(arg)
        nativeimpl.push_int(res)

    nativeimpl.push_func(wrapper, f)
    _exec_func.invoke()
    return nativeimpl.pop_int()


def stash_func(f):  # f: int -> int
    def wrapper():
        arg = nativeimpl.pop_int()
        res = f(arg)
        nativeimpl.push_int(res)

    nativeimpl.push_func(wrapper, f)
    _stash_func.invoke()
    # void return


def release_func():
    _release_func.invoke()


def return_stashed_func():
    _return_stashed_func.invoke()
    server_func = nativeimpl.pop_func()

    def wrapper(x):  # int -> int
        nativeimpl.push_int(x)
        server_func.invoke()
        return nativeimpl.pop_int()

    return wrapper


class IThing01:
    def some_method(self, x):
        pass

    def stash_thing(self, thing):
        pass

    def get_stashed_thing(self):
        pass

    def test_throw(self, do_throw):
        pass

    def on_timer_tick(self):
        pass

    # plumbing ======================
    def push(self, is_return):
        pass

    @staticmethod
    def pop():
        (obj_id, is_client_id) = nativeimpl.pop_instance()
        if is_client_id:
            return nativeimpl.ClientObject.get_by_id(obj_id)
        else:
            return ServerIThing01(obj_id)


class ServerIThing01(nativeimpl.ServerObject, IThing01):
    def __init__(self, obj_id):
        super().__init__(obj_id)

    def some_method(self, x):
        nativeimpl.push_int(x)
        _ithing_some_method.invoke(self.obj_id)
        return nativeimpl.pop_int()

    def stash_thing(self, thing):
        thing.push(False)
        _ithing_stash_thing.invoke(self.obj_id)

    def get_stashed_thing(self):
        _ithing_get_stashed_thing.invoke(self.obj_id)
        ret = IThing01.pop()
        nativeimpl.clear_server_safety_area()  # because we popped at least one interface-as-return-value
        return ret

    def test_throw(self, do_throw):
        nativeimpl.push_bool(do_throw)
        _ithing_test_throw.invoke_with_exceptions(self.obj_id)

    def on_timer_tick(self):
        _ithing_on_timer_tick.invoke(self.obj_id)

    # ======================================
    def push(self, is_return):
        self.server_push(is_return)


class ClientIThing01Base(nativeimpl.ClientObject, IThing01):
    def push(self, is_return):
        self.client_push()


def make_thing():
    _make_thing.invoke()
    ret = IThing01.pop()
    nativeimpl.clear_server_safety_area()  # because we popped at least one interface-as-return-value
    return ret


def operate_on_thing(thing, arg):
    nativeimpl.push_int(arg)
    thing.push(False)
    _operate_on_thing.invoke()
    return nativeimpl.pop_int()


def save_thing(thing):
    thing.push(False)
    _save_thing.invoke()


def get_saved_thing():
    _get_saved_thing.invoke()
    ret = IThing01.pop()
    nativeimpl.clear_server_safety_area()  # because we popped at least one interface-as-return-value
    return ret


def client_stash_test(thing):
    thing.push(False)
    _client_stash_test.invoke()


def test_negative(value):
    nativeimpl.push_int(value)
    _test_negative.invoke_with_exceptions()


def test_client_throw(thing):
    thing.push(False)
    _test_client_throw.invoke()  # it handles exceptions internally


def start_timer(thing, freq):
    nativeimpl.push_int(freq)
    thing.push(False)
    _start_timer.invoke()


def stop_timer():
    _stop_timer.invoke()


def dump_tables():
    nativeimpl.dump_tables()


@atexit.register
def shutdown():
    nativeimpl.shutdown()
