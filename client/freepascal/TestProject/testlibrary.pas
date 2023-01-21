unit TestLibrary;

{$mode ObjFPC}{$H+}

interface

uses
  Classes, SysUtils, NativeImplClient;

function Init(): Boolean;
procedure Shutdown();

// exported methods:
function Sum(a: Integer; b: Integer; c: Integer): Integer;
function AppendToString(str: String): String;
function ArraySum(values: Array of Integer): Integer;

implementation

var
  m: TModuleHandle;
  sumMethod: TModuleMethodHandle;
  appendToStringMethod: TModuleMethodHandle;
  arraySumMethod: TModuleMethodHandle;

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

end.

