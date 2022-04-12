package org.prefixed.nativeimpl;

import java.util.*;

public class NativeImpl {
    private static class NativeHandle {
        public long handle;

        public NativeHandle(long handle) {
            this.handle = handle;
        }
    }

    public static class ModuleHandle extends NativeHandle {
        public ModuleHandle(long handle) {
            super(handle);
        }
    }

    public static class ModuleMethodHandle extends NativeHandle {
        public ModuleMethodHandle(long handle) {
            super(handle);
        }
    }

    public static class InterfaceHandle extends NativeHandle {
        public InterfaceHandle(long handle) {
            super(handle);
        }
    }

    public static class InterfaceMethodHandle extends NativeHandle {
        public InterfaceMethodHandle(long handle) {
            super(handle);
        }
    }

    public static class ExceptionHandle extends NativeHandle {
        public ExceptionHandle(long handle) {
            super(handle);
        }
    }

    private static final NativeMethods.Callbacks callbacks = new NativeMethods.Callbacks() {
        @Override
        public void clientFuncExec(int id) {
            System.out.printf("hello from clientFuncExec %d\n", id);
            FuncValue fv = clientFuncs.get(id);
            fv.wrapper.apply();
        }

        @Override
        public void clientFuncRelease(int id) {
            System.out.printf("hello from clientFuncRelease %d\n", id);
            FuncValue fv = clientFuncs.get(id);
            fv.refCount -= 1;
            if (fv.refCount == 0) {
                System.out.println(" - refcount 0, releasing");
                // remove from both tables, so GC isn't blocked from collecting
                clientFuncs.remove(id);
                clientFuncHashToFunc.remove(fv.funcHash);
            }
        }

        @Override
        public void clientMethodExec(long ifaceMethod, int objID) {
            System.out.printf("hello from clientMethodExec %d/%d\n", ifaceMethod, objID);
            MethodWrapper wrapper = methodWrappers.get(ifaceMethod);
            ClientObject obj = clientObjects.get(objID);
            wrapper.apply(obj);
        }

        @Override
        public void clientObjectRelease(int id) {
            System.out.printf("hello from clientObjectRelease %d\n", id);
            ClientObject obj = clientObjects.get(id);
            obj.release();
        }

        @Override
        public void clientClearSafetyArea() {
            System.out.println("####### hello from clientClearSafetyArea #########\n");
            safeArea.clear();
        }
    };

    public static void init() {
        NativeMethods.init(callbacks);
    }

    public static void shutdown() {
        NativeMethods.shutdown();
    }

    public static ModuleHandle getModule(String name) {
        long handle = NativeMethods.getModule(name);
        return new ModuleHandle(handle);
    }

    public static ModuleMethodHandle getModuleMethod(ModuleHandle mh, String name) {
        long handle = NativeMethods.getModuleMethod(mh.handle, name);
        return new ModuleMethodHandle(handle);
    }

    public static void invokeModuleMethod(ModuleMethodHandle method) {
        NativeMethods.invokeModuleMethod(method.handle);
    }

    public interface ExceptionBuilder {
        void buildAndThrow() throws Throwable;
    }

    private static final Map<Long, ExceptionBuilder> exceptionBuilderMap = new HashMap<>(); // map from native exception handle, to build+throw'er

    public static void invokeModuleMethodWithExceptions(ModuleMethodHandle method) throws Throwable {
        long e = NativeMethods.invokeModuleMethodWithExceptions(method.handle);
        if (e != 0) {
            ExceptionBuilder builder = exceptionBuilderMap.get(e);
            builder.buildAndThrow();
        }
    }

    public static void setExceptionBuilder(ExceptionHandle eh, ExceptionBuilder builder) {
        exceptionBuilderMap.put(eh.handle, builder);
    }

    public static InterfaceHandle getInterface(ModuleHandle mh, String name) {
        long handle = NativeMethods.getInterface(mh.handle, name);
        return new InterfaceHandle(handle);
    }

    public static InterfaceMethodHandle getInterfaceMethod(InterfaceHandle iface, String name) {
        long handle = NativeMethods.getInterfaceMethod(iface.handle, name);
        return new InterfaceMethodHandle(handle);
    }

    public static ExceptionHandle getException(ModuleHandle mh, String name) {
        long handle = NativeMethods.getException(mh.handle, name);
        return new ExceptionHandle(handle);
    }

    public static void invokeInterfaceMethod(InterfaceMethodHandle method, int serverID) {
        NativeMethods.invokeInterfaceMethod(method.handle, serverID);
    }

    public static void invokeInterfaceMethodWithExceptions(InterfaceMethodHandle method, int serverID) throws Throwable {
        long e = NativeMethods.invokeInterfaceMethodWithExceptions(method.handle, serverID);
        if (e != 0) {
            ExceptionBuilder builder = exceptionBuilderMap.get(e);
            builder.buildAndThrow();
        }
    }

    public static void pushBool(boolean value) {
        NativeMethods.pushBool(value);
    }

    public static boolean popBool() {
        return NativeMethods.popBool();
    }

    public static void pushInt(int x) {
        NativeMethods.pushInt(x);
    }

    public static int popInt() {
        return NativeMethods.popInt();
    }

    public interface FuncWrapper {
        void apply();
    }

    public interface MethodWrapper {
        void apply(ClientObject obj);
    }

    private static class FuncValue {
        int id;
        FuncWrapper wrapper;
        int funcHash;
        int refCount;

        FuncValue(int id, FuncWrapper wrapper, int funcHash) {
            this.id = id;
            this.wrapper = wrapper;
            this.funcHash = funcHash;
            refCount = 1;
        }
    }

    private static int nextFuncId = 1;
    private static final Map<Integer, FuncValue> clientFuncs = new HashMap<>(); // id -> funcvalue
    private static final Map<Integer, FuncValue> clientFuncHashToFunc = new HashMap<>(); // hash int -> funcvalue

    public static void pushFunc(FuncWrapper wrapper, int hash) {
        FuncValue fv;
        if (clientFuncHashToFunc.containsKey(hash)) {
            System.out.printf("client func hash contains hash %08X already!\n", hash);
            fv = clientFuncHashToFunc.get(hash);
            fv.refCount += 1;
        } else {
            fv = new FuncValue(nextFuncId++, wrapper, hash);
            clientFuncs.put(fv.id, fv);
            clientFuncHashToFunc.put(hash, fv);
        }
        NativeMethods.pushClientFunc(fv.id);
    }

    public static class ServerFunc {
        private final int id;

        public ServerFunc(int id) {
            this.id = id;
        }

        public void apply() {
            NativeMethods.execServerFunc(id);
        }

        @Override
        protected void finalize() throws Throwable {
            System.out.printf("finalizing/releasing ServerFunc %d\n", id);
            NativeMethods.releaseServerFunc(id);
        }
    }

    public static ServerFunc popFunc() {
        int id = NativeMethods.popServerFunc();
        return new ServerFunc(id);
    }

    public static class ClientObject {
        public int id = -1;
        public int refCount;

        protected void _push() {
            // send across
            if (id == -1) {
                id = nextObjectId++;
                refCount = 1;
                clientObjects.put(id, this);
            } else {
                // already exists
                refCount += 1;
            }
            NativeMethods.pushClientInst(id);
        }

        void release() {
            System.out.printf("ClientObject %d releasing\n", id);
            refCount -= 1;
            if (refCount == 0) {
                System.out.println(" - refcount zero, removing from object map");
                clientObjects.remove(id);
                id = -1; // should we re-use IDs? would complicate the logic in push() ...
            }
        }

        public static ClientObject getbyID(int id) {
            return clientObjects.get(id);
        }
    }

    private static int nextObjectId = 1;
    private static final Map<Integer, ClientObject> clientObjects = new HashMap<>();

    public static InstanceID popInstanceID() {
        return NativeMethods.popInstance();
    }

    public static class ServerObject {
        public final int id;
        private boolean released = false;

        public ServerObject(int id) {
            this.id = id;
        }

        protected void _push(boolean isReturn) {
            NativeMethods.pushServerInst(id);
            if (isReturn) {
                // move to safety area, until remote gives the OK
                // while in transit don't want to inadvertently signal to remote side that it's OK to destroy the object (in the unlikely event of a GC before then)
                safeArea.add(this);
            }
        }

        protected void release() {
            if (!released) {
                NativeMethods.releaseServerInst(id);
                released = true;
            }
        }

        @Override
        protected void finalize() throws Throwable {
            if (!released) {
                NativeMethods.releaseServerInst(id);
                released = true;
            }
        }
    }

    private static final Map<Long, MethodWrapper> methodWrappers = new HashMap<>();

    public static void setClientMethodWrapper(InterfaceMethodHandle method, MethodWrapper wrapper) {
        methodWrappers.put(method.handle, wrapper);
    }

    private static final List<ServerObject> safeArea = new LinkedList<>();

    public static void clearServerSafetyArea() {
        NativeMethods.clearServerSafetyArea();
    }

    public static void pushModuleConstants(ModuleHandle mh) {
        NativeMethods.pushModuleConstants(mh.handle);
    }

    public static void setException(ExceptionHandle eh) {
        NativeMethods.setException(eh.handle);
    }
}
