from cgi import test
import testlibrary
import time

result = testlibrary.sum(101, 202, 303)
print(f"result is: {result}")

res2 = testlibrary.exec_func(lambda x: x * 101)
print(f"res2 (exec_func) {res2}")
res2 = testlibrary.exec_func(lambda q: q + 123)
print(f"res2 (exec_func) {res2}")


def f(x):
    print(f"hello from python f({x})")
    return x + 1

testlibrary.stash_func(f)

q = testlibrary.return_stashed_func()

print("executing mystery q!!")
res = q(101)
print(f"q complete: {res}")

testlibrary.stash_func(f)
print("releasing ...")
testlibrary.release_func()

print("== module constants:")
print(f" - global01: {testlibrary.global01}")
print(f" - another_global: {testlibrary.another_global}")
print(f" - last_int_global: {testlibrary.last_int_global}")

with testlibrary.make_thing() as thing:
    res = thing.some_method(345)  # * 321
    print(f"res was: {res} (should be: {321 * 345})")

    res2 = testlibrary.operate_on_thing(thing, 31)
    print(f"res2 is: {res2} (should be: {321 * 31})")
# end with
print("----- end with #1 -----")


class MyIThing(testlibrary.ClientIThing01Base):
    def __init__(self):
        super().__init__()
        self.stashed = None

    def some_method(self, x):
        print(f"MyIThing.some_method! {x}")
        return (x + 1) * 11

    def stash_thing(self, thing):
        print(f"MyIThing.stash_thing stashing ({thing})")
        self.stashed = thing

    def get_stashed_thing(self):
        print(f"MyIThing.get_stashed_thing returning ({self.stashed})")
        ret = self.stashed
        self.stashed = None
        return ret

    def test_throw(self, do_throw):
        print(f"## Python: test_throw({do_throw})")
        if do_throw:
            raise testlibrary.InterfaceMethodException(7890)

    def on_timer_tick(self):
        print("Python: MyIThing.on_timer_tick()!!")

    def __exit__(self, exc_type, exc_val, exc_tb):
        print("%%%% MyIThing custom __exit__!!!!")


# thing2 = MyIThing()
with MyIThing() as thing2:
    res2 = thing2.some_method(3)

    testlibrary.save_thing(thing2)

    with testlibrary.get_saved_thing() as argh:
        argh.some_method(1001)
        testlibrary.client_stash_test(argh)

    print("point x reached")

    testlibrary.operate_on_thing(thing2, 451)  # remote invocation of some_method

    print("================ before test client throw")
    testlibrary.test_client_throw(thing2)
    print("================ after test client throw")

    print("starting timer...")
    testlibrary.start_timer(thing2, 2)

    print("sleeping 5 seconds...")
    time.sleep(10)

    testlibrary.stop_timer()

    print("before end with!!!")

# end with
print("----- end with2 -----")

res3 = thing2.some_method(3)
print(f"res3 is: {res3} (should be: {(3 + 1) * 11}")

testlibrary.test_negative(1)
try:
    print("trying test_negative(-1)...")
    testlibrary.test_negative(-1)
except testlibrary.NegativeException as e:
    print(f"caught NegativeError({e.value})")

testlibrary.dump_tables()
