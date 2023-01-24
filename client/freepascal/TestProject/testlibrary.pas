unit TestLibrary;

{$mode ObjFPC}{$H+}
//{$interfaces corba} // no COM interfaces

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

  { dependencies for TSomeIntFuncWrapper }

  TSomeIntFuncWrapper = class; // fwd decl
  TIntFuncMap = specialize TFPGMap<Pointer,TSomeIntFuncWrapper>;

  { TSomeIntFuncWrapper }
  TSomeIntFuncWrapper = class(TClientFuncValue)
    private
      _map: TIntFuncMap; static; // to make sure we're not re-wrapping a given "closure" again
      _f: ISomeIntFunc;          // lifetime managed by interface
    public
      class constructor Create;                                         // static init
      class function GetOrCreate(f: ISomeIntFunc): TSomeIntFuncWrapper; // static factory, checks for duplicates
      procedure Apply(); override;                                      // handles the stack operations to invoke interface-specific .Apply
      destructor Destroy(); override;
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

destructor TSomeIntFuncWrapper.Destroy;
begin
  inherited Destroy;
  // a given wrapper got destroyed, so remove its content from lookup table
  _map.Remove(Pointer(_f));
  writeln('TSomeIntFuncWrapper.Destroy!');
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
  if _map.TryGetData(Pointer(f), wrapper) then
  begin
    // 'wrapper' var already populated due to TryGetData above
  end else
  begin
    // need to create
    wrapper := TSomeIntFuncWrapper.Create;
    wrapper._f := f;
    _map.Add(Pointer(f), wrapper);
  end;
  // return
  result := wrapper;
end;


end.

