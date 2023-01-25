unit TestLibrary;

{$mode ObjFPC}{$H+}
//{$interfaces corba} // no COM interfaces

// suppress fgl inline warning shit
{$warn 3123 off} // "inherited" not yet supported inside inline procedure/function
{$warn 3124 off} // inlining disabled
{$warn 6058 off} // ... marked as inline is not inlined

interface

uses
  Classes, SysUtils, NativeImplClient; // , fgl;

type
  { ISomeIntFunc }
  ISomeIntFunc = interface
    function Apply(x: Integer): Integer;
  end;

  { IArrayFunc }
  IArrayFunc = interface
    function Apply(items: array of Integer): Integer;
  end;

procedure SomeIntFuncWrapper(f: IUnknown);
procedure ArrayFuncWrapper(f: IUnknown);

function Init(): Boolean;
procedure Shutdown();

// exported methods:
function Sum(a: Integer; b: Integer; c: Integer): Integer;
function AppendToString(str: String): String;
function ArraySum(values: Array of Integer): Integer;
function ApplyIntFunc(f: ISomeIntFunc; i: Integer): Integer;
function ApplyArrayFunc(f: IArrayFunc; numItems: Integer): Integer;

implementation

var
  m: TModuleHandle;
  sumMethod: TModuleMethodHandle;
  appendToStringMethod: TModuleMethodHandle;
  arraySumMethod: TModuleMethodHandle;
  intFuncMethod: TModuleMethodHandle;
  arrayFuncMethod: TModuleMethodHandle;

procedure SomeIntFuncWrapper(f: IUnknown);
var
  x, res: Integer;
begin
  x := NativeImplClient.PopInt32();
  res := ISomeIntFunc(f).Apply(x);
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
  arrayFuncMethod := NativeImplClient.GetModuleMethod(m, 'applyArrayFunc');
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

function ApplyIntFunc(f: ISomeIntFunc; i: Integer): Integer;
begin
  NativeImplClient.PushInt32(i);
  NativeImplClient.PushFuncValue(@SomeIntFuncWrapper, f);
  NativeImplClient.InvokeModuleMethod(intFuncMethod);
  result := NativeImplClient.PopInt32();
end;

function ApplyArrayFunc(f: IArrayFunc; numItems: Integer): Integer;
begin
  NativeImplClient.PushInt32(numItems);
  NativeImplClient.PushFuncValue(@ArrayFuncWrapper, f);
  NativeImplClient.InvokeModuleMethod(arrayFuncMethod);
  result := NativeImplClient.PopInt32();
end;

end.

