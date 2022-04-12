package org.prefixed.nativeimpl;

// to generate header, run this from /src dir: javah org.prefixed.nativeimpl.NativeMethods

public class NativeMethods {
    static public native void staticInit(); // java-specific: grab all the class/method handles etc
    static {
        // load DLL here
        System.loadLibrary("TestLibrary");
        staticInit();
    }

    interface Callbacks {
        void clientFuncExec(int id);
        void clientFuncRelease(int id);
        void clientMethodExec(long ifaceMethod, int objID);
        void clientObjectRelease(int id);
        void clientClearSafetyArea();
    }

    // actual client methods
    static public native int init(Callbacks callbacks);     // calls nativeImplInit, wires up to callbacks above
    static public native void shutdown(); // nativeImplShutdown

    // 'get' stuff
    static public native long getModule(String name);
    static public native long getModuleMethod(long module, String name);
    static public native long getInterface(long module, String name);
    static public native long getInterfaceMethod(long iface, String name);
    static public native long getException(long module, String name);
    static public native void pushModuleConstants(long module);

    static public native void invokeModuleMethod(long method);
    static public native long invokeModuleMethodWithExceptions(long method);

    static public native void pushBool(boolean value);
    static public native boolean popBool();

    static public native void pushInt(int x);
    static public native int popInt();

    static public native void pushClientFunc(int id);
    static public native int popServerFunc();

    static public native void execServerFunc(int id);
    static public native void releaseServerFunc(int id);

    static public native void invokeInterfaceMethod(long method, int serverID);
    static public native long invokeInterfaceMethodWithExceptions(long method, int serverID);

    static public native void pushClientInst(int id);
    static public native void pushServerInst(int id);

    static public native InstanceID popInstance();
    static public native void releaseServerInst(int id);

    static public native void clearServerSafetyArea();

    static public native void dumpTables();

    static public native void setException(long e);
}
