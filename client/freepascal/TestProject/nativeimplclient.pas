
unit NativeImplClient;

{$mode ObjFPC}{$H+}
//{$interfaces corba} // no COM interfaces

interface

uses
  Classes, SysUtils, NativeMethods, ClientResource, ClientFuncValue, ServerFuncVal;

type
  { THandle }
  generic THandle<T> = class
    private
      Handle: T;
    public
      constructor Create(h: T);
  end;

  TModuleHandle = specialize THandle<NativeMethods.TModuleRef>;
  TModuleMethodHandle = specialize THandle<NativeMethods.TModuleMethodRef>;
  TInterfaceHandle = specialize THandle<NativeMethods.TInterfaceRef>;
  TInterfaceMethodHandle = specialize THandle<NativeMethods.TInterfaceMethodRef>;

  TIntArray = Array of Integer;

function Init(): Boolean;
procedure Shutdown();
function GetModule(Name: String): TModuleHandle;
function GetModuleMethod(m: TModuleHandle; Name: String): TModuleMethodHandle;
function GetInterface(m: TModuleHandle; Name: String): TInterfaceHandle;
function GetInterfaceMethod(iface: TInterfaceHandle; Name: String): TInterfaceMethodHandle;

procedure InvokeModuleMethod(method: TModuleMethodHandle);
procedure InvokeInterfaceMethod(method: TInterfaceMethodHandle; objID: Integer);

procedure PushInt32(i: Integer);
function PopInt32(): Integer;
procedure PushString(str: String);
function PopString(): String;

procedure PushInt32Array(i: Array of Integer);
function PopInt32Array(): TIntArray;

function PopServerFuncVal(): TServerFuncVal;

procedure PushFuncValue(wrapper: TWrapperFunc; f: IUnknown);

function PopInstance(out isClientID: Boolean; out inSafetyArea: Boolean): Integer;

procedure ClearServerSafetyArea();
procedure ReleaseServerResource(id: Integer);

implementation

// CALLBACKS - need to be extern(C)! =========================================

procedure ClientFuncExec(id: Integer); cdecl;
var
  f: TClientFuncValue;
begin
  f := TClientFuncValue.GetByID(id);
  if f <> nil then
  begin
    writeln('ClientFuncExec: executing id ' + IntToStr(id));
    f.Apply();
  end else
  begin
    writeln('ClientFuncExec: unknown ID ' + IntToStr(id));
  end;
end;

procedure ClientMethodExec(method: TInterfaceMethodRef; objID: Integer); cdecl;
begin
end;

procedure ClientResourceRelease(id: Integer); cdecl;
var
  res: TClientResource;
begin
  res := TClientResource.GetByID(id);
  if res <> nil then
  begin
    res.Release();
  end else
  begin
    writeln('ClientResourceRelease: id not found: ' + IntToStr(id));
  end;
end;

procedure ClientClearSafetyArea(); cdecl;
begin
end;

// end callbacks ==============================================================

{ THandle }
constructor THandle.Create(h: T);
begin
  Handle := h;
end;

// main exports:

function Init: Boolean;
var
  InitResult: Integer;
begin
  InitResult := NativeMethods.nativeImplInit(@ClientFuncExec, @ClientMethodExec, @ClientResourceRelease, @ClientClearSafetyArea);
  Result := (InitResult = 0);
end;

procedure Shutdown;
begin
  NativeMethods.nativeImplShutdown();
end;

function GetModule(Name: String): TModuleHandle;
var
  rawHandle: NativeMethods.TModuleRef;
begin
  rawHandle := NativeMethods.getModule(PChar(Name));
  if rawHandle <> 0 then
  begin
    Result := TModuleHandle.Create(rawHandle);
  end else
  begin
    Result := nil;
  end;
end;

function GetModuleMethod(m: TModuleHandle; name: String): TModuleMethodHandle;
var
  rawHandle: NativeMethods.TModuleMethodRef;
begin
  rawHandle := NativeMethods.getModuleMethod(m.Handle, PChar(Name));
  if rawHandle <> 0 then
  begin
    Result := TModuleMethodHandle.Create(rawHandle);
  end else
  begin
    Result := nil;
  end;
end;

function GetInterface(m: TModuleHandle; name: String): TInterfaceHandle;
var
  rawHandle: NativeMethods.TInterfaceRef;
begin
  rawHandle := NativeMethods.getInterface(m.Handle, PChar(name));
  if rawHandle <> 0 then
  begin
    Result := TInterfaceHandle.Create(rawHandle);
  end;
end;

function GetInterfaceMethod(iface: TInterfaceHandle; name: String): TInterfaceMethodHandle;
var
  rawHandle: NativeMethods.TInterfaceMethodRef;
begin
  rawHandle := NativeMethods.getInterfaceMethod(iface.Handle, PChar(name));
  if rawHandle <> 0 then
  begin
    result := TInterfaceMethodHandle.Create(rawHandle);
  end;
end;

procedure InvokeModuleMethod(method: TModuleMethodHandle);
begin
  NativeMethods.invokeModuleMethod(method.Handle);
end;

procedure InvokeInterfaceMethod(method: TInterfaceMethodHandle; objID: Integer);
begin
  NativeMethods.invokeInterfaceMethod(method.Handle, objID);
end;

procedure PushInt32(i: Integer);
begin
  NativeMethods.pushInt32(i);
end;

function PopInt32: Integer;
begin
  Result := NativeMethods.popInt32();
end;

procedure PushString(str: String);
begin
  NativeMethods.pushString(PChar(str), length(str));
end;

function PopString: String;
var
  chars: PChar;
  length: NativeUInt;
begin
  NativeMethods.popString(@chars, @length);
  result := ANSIString(chars);
end;

procedure PushInt32Array(i: array of Integer);
begin
  NativeMethods.pushInt32Array(PInteger(i), Length(i));
end;

function PopInt32Array: TIntArray;
var
  values: PInteger;
  count: NativeUInt;
  arr: array of Integer;
begin
  NativeMethods.popInt32Array(@values, @count);
  arr := nil;
  setLength(arr, count);
  // no way to treat values+count as an array?
  Move(values^,Pointer(arr)^,count * sizeof(Integer));
  result := arr;
end;

function PopServerFuncVal: TServerFuncVal;
var
  id: Integer;
begin
  id := NativeMethods.popServerFunc();
  result := TServerFuncVal.Create(id);
end;

procedure PushFuncValue(wrapper: TWrapperFunc; f: IUnknown);
var
  funcValue: TClientFuncValue;
  closure: TClosureInfo;
begin
  closure.wrapper := wrapper;
  closure.f := f;
  funcValue := TClientFuncValue.GetOrCreate(closure);
  NativeMethods.pushClientFunc(funcValue._id);
end;

function PopInstance(out isClientID: Boolean; out inSafetyArea: Boolean): Integer;
begin
  result := NativeMethods.popInstance(@isClientID, @inSafetyArea);
end;

procedure ClearServerSafetyArea;
begin
  NativeMethods.clearServerSafetyArea();
end;

procedure ReleaseServerResource(id: Integer);
begin
  NativeMethods.releaseServerResource(id);
end;


end.
