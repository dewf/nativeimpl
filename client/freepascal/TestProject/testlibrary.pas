unit TestLibrary;

{$mode ObjFPC}{$H+}
//{$interfaces corba} // no COM interfaces

interface

uses
  Classes, SysUtils, NativeImplClient, ServerFuncVal, ServerObject;

type
  { ISomeIntFunc }
  ISimpleIntFunc = interface
    function Apply(x: Integer): Integer;
  end;

  { IArrayFunc }
  IArrayFunc = interface
    function Apply(items: array of Integer): Integer;
  end;

  { IThing01 }
  ICoolThing = interface
    function DoSomething(x: Integer): Integer;
  end;

// exported methods:

function Init(): Boolean;
procedure Shutdown();

function Sum(a: Integer; b: Integer; c: Integer): Integer;
function AppendToString(str: String): String;
function ArraySum(values: Array of Integer): Integer;

function ApplyIntFunc(f: ISimpleIntFunc; i: Integer): Integer;
procedure StashFunc(f: ISimpleIntFunc);
procedure ClearedStashedFunc();

function MakeIntFunc(): ISimpleIntFunc;

function ApplyArrayFunc(f: IArrayFunc; numItems: Integer): Integer;

function MakeThing(): ICoolThing;

implementation

type
  { TServerIntFuncWrapper }
  TServerIntFuncWrapper = class; // fwd
  TServerIntFuncWrapperBase = specialize TServerFuncWrapperBase<ISimpleIntFunc, TServerIntFuncWrapper>;
  TServerIntFuncWrapper = class(TServerIntFuncWrapperBase, ISimpleIntFunc)
    public
      function Apply(x: Integer): Integer;
  end;

  { TServerCoolThing }
  TServerCoolThing = class(TInterfacedObject, ICoolThing)
    private
      _obj: TServerObject;
    public
      constructor Create(id: Integer);
      function DoSomething(x: Integer): Integer;
      destructor Destroy; override;
  end;

// private stuff

var
  m: TModuleHandle;
  sumMethod: TModuleMethodHandle;
  appendToStringMethod: TModuleMethodHandle;
  arraySumMethod: TModuleMethodHandle;
  intFuncMethod: TModuleMethodHandle;
  stashFuncMethod: TModuleMethodHandle;
  clearStashedFuncMethod: TModuleMethodHandle;
  arrayFuncMethod: TModuleMethodHandle;
  makeIntFuncMethod: TModuleMethodHandle;

  makeThingMethod: TModuleMethodHandle;

  // IThing01 methods
  coolThing__iface: TInterfaceHandle;
  coolThing__doSomethingMethod: TInterfaceMethodHandle;

// function val wrappers ===========================
procedure SimpleIntFuncWrapper(f: IUnknown);
var
  x, res: Integer;
begin
  x := NativeImplClient.PopInt32();
  res := ISimpleIntFunc(f).Apply(x);
  NativeImplClient.PushInt32(res);
end;

procedure ArrayFuncWrapper(f: IUnknown);
var
  items: array of Integer;
  res: Integer;
begin
  items := NativeImplClient.PopInt32Array();
  res := IArrayFunc(f).Apply(items);
  NativeImplClient.PushInt32(res);
end;
// end func val wrappers ===========================

// private functions

function ICoolThing__Pop: ICoolThing;
var
  id: Integer;
  isClientId, inSafetyArea: Boolean;
begin
  id := NativeImplClient.PopInstance(isClientId, inSafetyArea);
  if isClientId then
  begin
    // short circuit
    writeln('ICoolThing__Pop: client pop not yet implemented');
    result := nil;
    // clear safety area, only a client thing? I forget
  end else
  begin
    // create server object
    result := TServerCoolThing.Create(id);
    // safety area??
  end;
end;

{ TServerCoolThing }
constructor TServerCoolThing.Create(id: Integer);
begin
  _obj := TServerObject.Create(id);
end;

function TServerCoolThing.DoSomething(x: Integer): Integer;
begin
  NativeImplClient.PushInt32(x);
  _obj.InvokeMethod(coolThing__doSomethingMethod);
  result := NativeImplClient.PopInt32();
end;

destructor TServerCoolThing.Destroy;
begin
  _obj.Free;
  inherited Destroy;
end;

function TServerIntFuncWrapper.Apply(x: Integer): Integer;
begin
  NativeImplClient.PushInt32(x);
  _f.Invoke();
  result := NativeImplClient.PopInt32();
end;

// end private functions

// begin exported functions ==================================================

function Init: Boolean;
begin
  result := NativeImplClient.Init();
  if not result then
  begin
    exit(false);
  end;
  m := NativeImplClient.GetModule('TestLibrary');
  sumMethod := NativeImplClient.GetModuleMethod(m, 'sum');
  appendToStringMethod := NativeImplClient.GetModuleMethod(m, 'appendToString');
  arraySumMethod := NativeImplClient.GetModuleMethod(m, 'arraySum');
  intFuncMethod := NativeImplClient.GetModuleMethod(m, 'applyIntFunc');
  stashFuncMethod := NativeImplClient.GetModuleMethod(m, 'stashFunc');
  clearStashedFuncMethod := NativeImplClient.GetModuleMethod(m, 'clearStashedFunc');
  arrayFuncMethod := NativeImplClient.GetModuleMethod(m, 'applyArrayFunc');
  makeIntFuncMethod := NativeImplClient.GetModuleMethod(m, 'makeIntFunc');

  makeThingMethod := NativeImplClient.GetModuleMethod(m, 'makeThing');

  coolThing__iface := NativeImplClient.GetInterface(m, 'CoolThing');
  coolThing__doSomethingMethod := NativeImplClient.GetInterfaceMethod(coolThing__iface, 'doSomething');

end;

procedure Shutdown;
begin
  NativeImplClient.Shutdown();
end;

function Sum(a: Integer; b: Integer; c: Integer): Integer;
begin
  NativeImplClient.PushInt32(c);
  NativeImplClient.PushInt32(b);
  NativeImplClient.PushInt32(a);
  NativeImplClient.InvokeModuleMethod(sumMethod);
  result := NativeImplClient.PopInt32();
end;

function AppendToString(str: String): String;
begin
  NativeImplClient.PushString(str);
  NativeImplClient.InvokeModuleMethod(appendToStringMethod);
  result := NativeImplClient.PopString();
end;

function ArraySum(values: array of Integer): Integer;
begin
  NativeImplClient.PushInt32Array(values);
  NativeImplClient.InvokeModuleMethod(arraySumMethod);
  result := NativeImplClient.PopInt32();
end;

function ApplyIntFunc(f: ISimpleIntFunc; i: Integer): Integer;
begin
  NativeImplClient.PushInt32(i);
  NativeImplClient.PushFuncValue(@SimpleIntFuncWrapper, f);
  NativeImplClient.InvokeModuleMethod(intFuncMethod);
  result := NativeImplClient.PopInt32();
end;

procedure StashFunc(f: ISimpleIntFunc);
begin
  NativeImplClient.PushFuncValue(@SimpleIntFuncWrapper, f);
  NativeImplClient.InvokeModuleMethod(stashFuncMethod);
end;

procedure ClearedStashedFunc;
begin
  NativeImplClient.InvokeModuleMethod(clearStashedFuncMethod);
end;

function MakeIntFunc(): ISimpleIntFunc;
begin
  NativeImplClient.InvokeModuleMethod(makeIntFuncMethod);
  result := TServerIntFuncWrapper.Pop();
  //NativeImplClient.ClearServerSafetyArea(); // ??
end;

function ApplyArrayFunc(f: IArrayFunc; numItems: Integer): Integer;
begin
  NativeImplClient.PushInt32(numItems);
  NativeImplClient.PushFuncValue(@ArrayFuncWrapper, f);
  NativeImplClient.InvokeModuleMethod(arrayFuncMethod);
  result := NativeImplClient.PopInt32();
end;

function MakeThing: ICoolThing;
begin
  NativeImplClient.InvokeModuleMethod(makeThingMethod);
  result := ICoolThing__Pop();
end;

// module end
end.

