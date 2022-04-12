const nativeMethods = require('./testlibrary_native.node');
console.log('testlibrary_native', nativeMethods);

var nextFuncId = 1;
const clientFuncs = new Map() // map from id -> wrapper
const clientFuncHashToFunc = new Map() // raw func hash -> clientFunc id

function addFuncVal(wrapper, uniqueKey) {
    if (!clientFuncHashToFunc.has(uniqueKey)) {
        // create new
        const id = nextFuncId++;
        clientFuncHashToFunc[uniqueKey] = id;
        clientFuncs[id] = { wrapper: wrapper, key: uniqueKey, refCount: 1 }
        return id;
    } else {
        // just increment refcount
        const id = clientFuncHashToFunc[uniqueKey];
        const f = clientFuncs[id];
        // increment refcount + rewrite
        f.refCount += 1;
        clientFuncs[id] = f;
        return id;
    }
}

function clientFuncExec(id) {
    console.log("node: clientFuncExec(): ", id);
    clientFuncs[id].wrapper();
}
function clientFuncRelease(id) {
    console.log("node: clientFuncRelease(): ", id);
    const funcVal = clientFuncs[id];
    funcVal.refCount -= 1;
    if (funcVal.refCount === 0) {
        clientFuncHashToFunc.delete(funcVal.key);
        clientFuncs.delete(id);
        console.log("nodejs removed client func: " + id);
    } else {
        // re-store with updated refcount
        clientFuncs[id] = funcVal;
        console.log("nodejs updated refcount func id:" + id + ", count now: " + funcVal.refCount);
    }
}
function clientMethodExec(method, objId) {
    console.log("node: clientMethodExec(): ", method, objId);
}
function clientObjectRelease(id) {
    console.log("node: clientObjectRelease(): ", id);
}
function clientClearSafetyArea() {
    console.log("node: clientClearSafetyArea()");
}

// actual library begins here ==============================

// don't forget module constants

nativeMethods.init(clientFuncExec, clientFuncRelease, clientMethodExec, clientObjectRelease, clientClearSafetyArea);

const _module = nativeMethods.getModule("TestLibrary");
const _sumFunc = nativeMethods.getModuleMethod(_module, "sum|iii");
const _execFunc01 = nativeMethods.getModuleMethod(_module, "execFunc01|(i->i)");
const _stashFunc = nativeMethods.getModuleMethod(_module, "stashFunc|(i->i)");
const _releaseFunc = nativeMethods.getModuleMethod(_module, "releaseFunc|");
const _returnStashedFunc = nativeMethods.getModuleMethod(_module, "returnStashedFunc|");

function shutdown() {
    console.log('shutting down ...');
    nativeMethods.shutdown();
}

function sumFunc(a, b, c) {
    nativeMethods.pushInt(a)
    nativeMethods.pushInt(b)
    nativeMethods.pushInt(c)
    nativeMethods.invokeModuleMethod(_sumFunc)
    return nativeMethods.popInt()
}

function execFunc01(intFunc) {
    function callbackWrapper() {
        let x = nativeMethods.popInt();
        let res = intFunc(x);
        nativeMethods.pushInt(res);
    }
    nativeMethods.pushClientFunc(addFuncVal(callbackWrapper, intFunc));
    nativeMethods.invokeModuleMethod(_execFunc01);
    return nativeMethods.popInt();
}

function stashFunc(intFunc) {
    function callbackWrapper() {
        let x = nativeMethods.popInt();
        let res = intFunc(x);
        nativeMethods.pushInt(res);
    }
    nativeMethods.pushClientFunc(addFuncVal(callbackWrapper, intFunc));
    nativeMethods.invokeModuleMethod(_stashFunc);
}

function releaseFunc() {
    nativeMethods.invokeModuleMethod(_releaseFunc);
}

// function returnStashedFunc() {
//     nativeMethods.invokeModuleMethod(_returnStashedFunc);
//     const id = nativeMethods.popServerFunc();
// }

module.exports = { shutdown, sumFunc, execFunc01, stashFunc, releaseFunc };
