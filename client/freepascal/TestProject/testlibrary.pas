unit TestLibrary;

{$mode ObjFPC}{$H+}
{$interfaces corba} // no COM interfaces

// suppress fgl inline warning shit
{$warn 3123 off} // "inherited" not yet supported inside inline procedure/function
{$warn 3124 off} // inlining disabled
{$warn 6058 off} // ... marked as inline is not inlined

interface

uses
  Classes, SysUtils, NativeImplClient, fgl;

type

  { ISomeIntFunc }

  ISomeIntFunc = interface
    function Apply(x: Integer): Integer;
  end;

  TSomeIntFuncWrapper = class; // fwd decl

  TIntFuncMap = specialize TFPGMap<Pointer,TSomeIntFuncWrapper>;

  { TSomeIntFuncWrapper }
  TSomeIntFuncWrapper = class(IFunctionValue)
    private
      _map: TIntFuncMap; static;
      _f: ISomeIntFunc;
    public
      class constructor Create; // static init
      class function GetOrCreate(f: ISomeIntFunc): TSomeIntFuncWrapper;
      procedure Apply();
  end;

function Init(): Boolean;
procedure Shutdown();

// exported methods:
function Sum(a: Integer; b: Integer; c: Integer): Integer;
function AppendToString(str: String): String;
function ArraySum(values: Array of Integer): Integer;
function ApplyIntFunc(f: ISomeIntFunc; i: Integer): Integer;

implementation

var
  m: TModuleHandle;
  sumMethod: TModuleMethodHandle;
  appendToStringMethod: TModuleMethodHandle;
  arraySumMethod: TModuleMethodHandle;
  intFuncMethod: TModuleMethodHandle;

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

function ArraySum(values: Array of Integer): Integer;
begin
  NativeImplClient.PushInt32Array(values);
  NativeImplClient.InvokeModuleMethod(arraySumMethod);
  result := NativeImplClient.PopInt32();
end;

function ApplyIntFunc(f: ISomeIntFunc; i: Integer): Integer;
var
  wrapper: TSomeIntFuncWrapper;
begin
  wrapper := TSomeIntFuncWrapper.GetOrCreate(f);
  NativeImplClient.PushInt32(i);
  NativeImplClient.PushFuncValue(wrapper);
  NativeImplClient.InvokeModuleMethod(intFuncMethod);
  result := NativeImplClient.PopInt32();
end;

{ TSomeIntFuncWrapper }

procedure TSomeIntFuncWrapper.Apply;
var
  x, res: Integer;
begin
  x := NativeImplClient.PopInt32();
  res := _f.Apply(x);
  NativeImplClient.PushInt32(res);
end;

class constructor TSomeIntFuncWrapper.Create;
begin
  _map := TIntFuncMap.Create();
end;

class function TSomeIntFuncWrapper.GetOrCreate(f: ISomeIntFunc): TSomeIntFuncWrapper;
var
  wrapper: TSomeIntFuncWrapper;
begin
  // check to see if this particular closure already exists, return if so
  // otherwise return a new wrapper
  if _map.TryGetData(@f, wrapper) then
  begin
    wrapper := TSomeIntFuncWrapper.Create;
    wrapper._f := f;
  end else
  begin
    // else already stored in 'wrapper', due to TryGetData
    // but update retain count
  end;
  result := wrapper;
end;


end.

