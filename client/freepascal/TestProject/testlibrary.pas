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

  { TSomeIntFuncWrapper }
  TSomeIntFuncWrapper = class; // fwd
  TSomeIntFuncWrapperBase = specialize TGenericFuncWrapper<ISomeIntFunc,TSomeIntFuncWrapper>;
  TSomeIntFuncWrapper = class (TSomeIntFuncWrapperBase)
    public
      procedure Apply(); override;
  end;

  { IArrayFunc }
  IArrayFunc = interface
    function Apply(items: array of Integer): Integer;
  end;

  { TArrayFuncWrapper }
  TArrayFuncWrapper = class; // fwd decl
  TArrayFuncWrapperBase = specialize TGenericFuncWrapper<IArrayFunc,TArrayFuncWrapper>;
  TArrayFuncWrapper = class(TArrayFuncWrapperBase)
    public
      procedure Apply(); override;
  end;

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
var
  wrapper: TSomeIntFuncWrapper;
begin
  wrapper := TSomeIntFuncWrapper.GetOrCreate(f);
  NativeImplClient.PushInt32(i);
  NativeImplClient.PushFuncValue(wrapper);
  NativeImplClient.InvokeModuleMethod(intFuncMethod);
  result := NativeImplClient.PopInt32();
end;

function ApplyArrayFunc(f: IArrayFunc; numItems: Integer): Integer;
var
  wrapper: TArrayFuncWrapper;
begin
  wrapper := TArrayFuncWrapper.GetOrCreate(f);
  NativeImplClient.PushInt32(numItems);
  NativeImplClient.PushFuncValue(wrapper);
  NativeImplClient.InvokeModuleMethod(arrayFuncMethod);
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

{ TArrayFuncWrapper }

procedure TArrayFuncWrapper.Apply;
var
  items: array of Integer;
  res: Integer;
begin
  // no need for super method
  items := NativeImplClient.PopInt32Array();
  res := _f.Apply(items);
  NativeImplClient.PushInt32(res);
end;

end.

