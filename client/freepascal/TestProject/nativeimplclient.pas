
unit NativeImplClient;

{$mode ObjFPC}{$H+}

interface

uses
  Classes, SysUtils, NativeMethods;

type

  { TModuleHandle }

  TModuleHandle = class
  private
    Handle: NativeMethods.TModuleRef;
  public
    constructor Create(h: NativeMethods.TModuleRef);
  end;

  { TModuleMethodHandle }

  TModuleMethodHandle = class
  private
    Handle: NativeMethods.TModuleMethodRef;
  public
    constructor Create(h: NativeMethods.TModuleRef);
  end;

function Init(): Boolean;
procedure Shutdown();
function GetModule(Name: String): TModuleHandle;
function GetModuleMethod(m: TModuleHandle; Name: String): TModuleMethodHandle;
procedure InvokeModuleMethod(method: TModuleMethodHandle);
procedure PushInt32(i: Integer);
function PopInt32(): Integer;
procedure PushString(str: String);
function PopString(): String;
procedure PushInt32Array(i: Array of Integer);

implementation

// CALLBACKS - need to be extern(C)! =========================================

procedure ClientFuncExec(id: Integer); cdecl;
begin
end;

procedure ClientMethodExec(method: TInterfaceMethodRef; objID: Integer); cdecl;
begin
end;

procedure ClientResourceRelease(id: Integer); cdecl;
begin
end;

procedure ClientClearSafetyArea(); cdecl;
begin
end;

// end callbacks ==============================================================

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

function GetModuleMethod(m: TModuleHandle; Name: String): TModuleMethodHandle;
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

procedure InvokeModuleMethod(method: TModuleMethodHandle);
begin
  NativeMethods.invokeModuleMethod(method.Handle);
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

{ TModuleMethodHandle }

constructor TModuleMethodHandle.Create(h: NativeMethods.TModuleRef);
begin
  self.Handle := h;
end;

{ TModuleHandle }

constructor TModuleHandle.Create(h: NativeMethods.TModuleRef);
begin
  self.Handle := h;
end;

end.
