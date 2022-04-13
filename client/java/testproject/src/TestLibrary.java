import org.prefixed.nativeimpl.NativeImpl;
import org.prefixed.nativeimpl.InstanceID;
import org.prefixed.nativeimpl.NativeMethods;

public class TestLibrary {
    private static final NativeImpl.ModuleMethodHandle _sum;
    private static final NativeImpl.ModuleMethodHandle _execFunc01;
    private static final NativeImpl.ModuleMethodHandle _stashFunc;
    private static final NativeImpl.ModuleMethodHandle _releaseFunc;
    private static final NativeImpl.ModuleMethodHandle _returnStashedFunc;

    private static final NativeImpl.InterfaceHandle _ithing01;
    private static final NativeImpl.InterfaceMethodHandle _ithing01_someMethod;
    private static final NativeImpl.InterfaceMethodHandle _ithing01_stashThing;
    private static final NativeImpl.InterfaceMethodHandle _ithing01_getStashedThing;
    private static final NativeImpl.InterfaceMethodHandle _ithing01_testThrow;
    private static final NativeImpl.InterfaceMethodHandle _ithing01_onTimerTick;

    private static final NativeImpl.ModuleMethodHandle _makeThing;
    private static final NativeImpl.ModuleMethodHandle _operateOnThing;

    private static final NativeImpl.ModuleMethodHandle _saveThing;
    private static final NativeImpl.ModuleMethodHandle _getSavedThing;
    private static final NativeImpl.ModuleMethodHandle _clientStashTest;

    private static final NativeImpl.ExceptionHandle _negativeException;
    private static final NativeImpl.ExceptionHandle _interfaceMethodException;

    private static final NativeImpl.ModuleMethodHandle _testNegative;
    private static final NativeImpl.ModuleMethodHandle _testClientThrow;
    private static final NativeImpl.ModuleMethodHandle _startTimer;
    private static final NativeImpl.ModuleMethodHandle _stopTimer;

    // module constants
    public static int global01;
    public static int anotherGlobal;
    public static int lastIntGlobal;

    static {
        NativeImpl.init();

        NativeImpl.ModuleHandle mh = NativeImpl.getModule("TestLibrary");
        _sum = NativeImpl.getModuleMethod(mh, "sum");
        _execFunc01 = NativeImpl.getModuleMethod(mh, "execFunc01");
        _stashFunc = NativeImpl.getModuleMethod(mh, "stashFunc");
        _releaseFunc = NativeImpl.getModuleMethod(mh, "releaseFunc");
        _returnStashedFunc = NativeImpl.getModuleMethod(mh, "returnStashedFunc");

        _makeThing = NativeImpl.getModuleMethod(mh, "makeThing");
        _operateOnThing = NativeImpl.getModuleMethod(mh, "operateOnThing");

        _saveThing = NativeImpl.getModuleMethod(mh, "saveThing");
        _getSavedThing = NativeImpl.getModuleMethod(mh, "getSavedThing");
        _clientStashTest = NativeImpl.getModuleMethod(mh, "clientStashTest");

        _testNegative = NativeImpl.getModuleMethod(mh, "testNegative");
        _testClientThrow = NativeImpl.getModuleMethod(mh, "testClientThrow");
        _startTimer = NativeImpl.getModuleMethod(mh, "startTimer");
        _stopTimer = NativeImpl.getModuleMethod(mh, "stopTimer");

        // exceptions
        _negativeException = NativeImpl.getException(mh, "NegativeException");
        NativeImpl.setExceptionBuilder(_negativeException, NegativeException.builder);
        _interfaceMethodException = NativeImpl.getException(mh, "InterfaceMethodException");
        NativeImpl.setExceptionBuilder(_interfaceMethodException, InterfaceMethodException.builder);

        // ithing methods
        _ithing01 = NativeImpl.getInterface(mh, "IThing01");
        _ithing01_someMethod = NativeImpl.getInterfaceMethod(_ithing01, "someMethod");
        _ithing01_stashThing = NativeImpl.getInterfaceMethod(_ithing01, "stashThing");
        _ithing01_getStashedThing = NativeImpl.getInterfaceMethod(_ithing01, "getStashedThing");
        _ithing01_testThrow = NativeImpl.getInterfaceMethod(_ithing01, "testThrow");
        _ithing01_onTimerTick = NativeImpl.getInterfaceMethod(_ithing01, "onTimerTick");

        // register interface method handlers (wrappers)
        NativeImpl.setClientMethodWrapper(_ithing01_someMethod, obj -> {
            IThing01 thing = (IThing01) obj;
            int arg = NativeImpl.popInt();
            int res = thing.someMethod(arg);
            NativeImpl.pushInt(res);
        });

        NativeImpl.setClientMethodWrapper(_ithing01_stashThing, obj -> {
            IThing01 thing = (IThing01) obj;
            IThing01 arg = IThing01.pop();
            thing.stashThing(arg);
        });

        NativeImpl.setClientMethodWrapper(_ithing01_getStashedThing, obj -> {
            IThing01 thing = (IThing01) obj;
            IThing01 res = thing.getStashedThing();
            res.push(true);
        });

        NativeImpl.setClientMethodWrapper(_ithing01_testThrow, obj -> {
            IThing01 thing = (IThing01) obj;
            boolean arg = NativeImpl.popBool();
            try {
                thing.testThrow(arg);
            } catch (InterfaceMethodException e) {
                // convert to remote exception
                e.push();
                NativeImpl.setException(_interfaceMethodException);
            }
        });

        NativeImpl.setClientMethodWrapper(_ithing01_onTimerTick, obj -> {
            IThing01 thing = (IThing01) obj;
            thing.onTimerTick();
        });

        // set module constants
        NativeImpl.pushModuleConstants(mh);
        global01 = NativeImpl.popInt();
        anotherGlobal = NativeImpl.popInt();
        lastIntGlobal = NativeImpl.popInt();
    }

    public static int sum(int a, int b, int c) {
        NativeImpl.pushInt(c);
        NativeImpl.pushInt(b);
        NativeImpl.pushInt(a);
        NativeImpl.invokeModuleMethod(_sum);
        return NativeImpl.popInt();
    }

    interface IntFunc {
        int apply(int a);
    }

    public static int execFunc01(IntFunc func) {
        NativeImpl.FuncWrapper wrapper = () -> {
            int a = NativeImpl.popInt();
            NativeImpl.pushInt(func.apply(a));
        };
        NativeImpl.pushFunc(wrapper, func.hashCode()); // does the hash code work?
        NativeImpl.invokeModuleMethod(_execFunc01);
        return NativeImpl.popInt();
    }

    public static void stashFunc(IntFunc func) {
        NativeImpl.FuncWrapper wrapper = () -> {
            int a = NativeImpl.popInt();
            NativeImpl.pushInt(func.apply(a));
        };
        NativeImpl.pushFunc(wrapper, func.hashCode());
        NativeImpl.invokeModuleMethod(_stashFunc);
    }

    public static void releaseFunc() {
        NativeImpl.invokeModuleMethod(_releaseFunc);
    }

    public static IntFunc returnStashedFunc() {
        NativeImpl.invokeModuleMethod(_returnStashedFunc);
        NativeImpl.ServerFunc sf = NativeImpl.popFunc();
        return a -> {
            NativeImpl.pushInt(a);
            sf.apply();
            return NativeImpl.popInt();
        };
    }

    public static class NegativeException extends Throwable {
        public int value;
        public NegativeException(int value) {
            this.value = value;
        }
        public void push() {
            NativeMethods.pushInt(value);
        }
        public static final NativeImpl.ExceptionBuilder builder = () -> {
            throw new NegativeException(NativeMethods.popInt());
        };
    }

    public static class InterfaceMethodException extends Throwable {
        public int value;
        public InterfaceMethodException(int value) {
            this.value = value;
        }
        public void push() {
            NativeMethods.pushInt(value);
        }
        public static final NativeImpl.ExceptionBuilder builder = () -> {
            throw new InterfaceMethodException(NativeMethods.popInt());
        };
    }

    interface IThing01 extends AutoCloseable {
        int someMethod(int x);
        void stashThing(IThing01 thing2);
        IThing01 getStashedThing();
        void testThrow(boolean doThrow) throws InterfaceMethodException;
        void onTimerTick();

        // plumbing: =====================
        void push(boolean isReturn);

        static IThing01 pop() {
            InstanceID inst = NativeImpl.popInstanceID();
            if (inst.isClientID) {
                return ClientIThing01.getByID(inst.id);
            } else {
                return new ServerIThing01(inst.id);
            }
        }
    }

    public abstract static class ClientIThing01 extends NativeImpl.ClientObject implements IThing01 {
        static ClientIThing01 getByID(int id) {
            return (ClientIThing01) NativeImpl.ClientObject.getbyID(id);
        }

        @Override
        public void close() throws Exception {
            // default close does nothing - override down the line if you care
            System.out.println("ClientIThing01 - default close()");
        }

        @Override
        public void push(boolean isReturn) {
            _push();
        }
    }

    public static class ServerIThing01 extends NativeImpl.ServerObject implements IThing01 {
        public ServerIThing01(int id) {
            super(id);
        }

        @Override
        public int someMethod(int x) {
            NativeImpl.pushInt(x);
            NativeImpl.invokeInterfaceMethod(_ithing01_someMethod, id);
            return NativeImpl.popInt();
        }

        @Override
        public void stashThing(IThing01 thing2) {
            thing2.push(false);
            NativeImpl.invokeInterfaceMethod(_ithing01_stashThing, id);
        }

        @Override
        public IThing01 getStashedThing() {
            NativeImpl.invokeInterfaceMethod(_ithing01_getStashedThing, id);
            IThing01 ret = IThing01.pop();
            // any time there is even a single interface passed across (as a return value), we need to clearServerSafetyArea ONCE at the end of the method
            NativeImpl.clearServerSafetyArea();
            return ret;
        }

        @Override
        public void testThrow(boolean doThrow) throws InterfaceMethodException {
            NativeImpl.pushBool(doThrow);
            try {
                NativeImpl.invokeInterfaceMethodWithExceptions(_ithing01_testThrow, id);
            } catch (Throwable e) {
                // rethrow as specific type, sigh
                throw (InterfaceMethodException) e;
            }
        }

        @Override
        public void onTimerTick() {
            NativeImpl.invokeInterfaceMethod(_ithing01_onTimerTick, id);
        }

        // === plumbing =======================================

        @Override
        public void close() throws Exception {
            release();
        }

        @Override
        public void push(boolean isReturn) {
            _push(isReturn);
        }
    }

    public static IThing01 makeThing() {
        NativeImpl.invokeModuleMethod(_makeThing);
        IThing01 ret = IThing01.pop(); // in this particular instance we know the remote side is only dealing with a server object, but in general we wouldn't know
        // any time there is even a single interface passed across (as a return value), we need to clearServerSafetyArea ONCE at the end of the method
        NativeImpl.clearServerSafetyArea();
        return ret;
    }

    public static void operateOnThing(IThing01 thing, int arg) {
        NativeImpl.pushInt(arg);
        thing.push(false);
        NativeImpl.invokeModuleMethod(_operateOnThing);
    }

    public static void saveThing(IThing01 thing) {
        thing.push(false);
        NativeImpl.invokeModuleMethod(_saveThing);
    }

    public static IThing01 getSavedThing() {
        NativeImpl.invokeModuleMethod(_getSavedThing);
        IThing01 ret = IThing01.pop();
        // any time there is even a single interface passed across (as a return value), we need to clearServerSafetyArea ONCE at the end of the method
        NativeImpl.clearServerSafetyArea();
        return ret;
    }

    public static void clientStashTest(IThing01 thing) {
        thing.push(false);
        NativeImpl.invokeModuleMethod(_clientStashTest);
    }

    public static void testNegative(int i) throws NegativeException {
        NativeImpl.pushInt(i);
        try {
            NativeImpl.invokeModuleMethodWithExceptions(_testNegative);
        } catch (Throwable e) {
            // rethrow as specific, sigh
            throw (NegativeException) e;
        }
    }

    public static void testClientThrow(IThing01 thing) {
        thing.push(false);
        NativeImpl.invokeModuleMethod(_testClientThrow);
    }

    public static void startTimer(IThing01 handler, int freq) {
        NativeImpl.pushInt(freq);
        handler.push(false);
        NativeImpl.invokeModuleMethod(_startTimer);
    }

    public static void stopTimer() {
        NativeImpl.invokeModuleMethod(_stopTimer);
    }

    // ===================================

    static void shutdown() {
        NativeImpl.shutdown();
    }
}
