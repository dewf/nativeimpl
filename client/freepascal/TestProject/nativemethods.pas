unit NativeMethods;

{$mode ObjFPC}{$H+}

interface

uses
  Classes, SysUtils;

type
  TModuleRef = NativeUInt;
  TModuleMethodRef = NativeUInt;
  TInterfaceRef = NativeUInt;
  TInterfaceMethodRef = NativeUInt;
  TObjectRef = NativeUInt;
  TExceptionRef = NativeUInt;

  TClientFuncExec = procedure(id: Integer); cdecl;
  TClientMethodExec = procedure(method: TInterfaceMethodRef; objID: Integer); cdecl;
  TClientResourceRelease = procedure(id: Integer); cdecl;
  TClientClearSafetyArea = procedure(); cdecl;

function nativeImplInit(
  clientFuncExec: TClientFuncExec;
  clientMethodExec: TClientMethodExec;
  clientResourceRelease: TClientResourceRelease;
  clientClearSafetyArea: TClientClearSafetyArea): Integer; cdecl; external 'TestLibrary.dll';

procedure nativeImplShutdown(); cdecl; external 'TestLibrary.dll';

function getModule(name: PChar): TModuleRef; cdecl; external 'TestLibrary.dll';
function getModuleMethod(m: TModuleRef; name: PChar): TModuleMethodRef; cdecl; external 'TestLibrary.dll';

procedure invokeModuleMethod(method: TModuleMethodRef); cdecl; external 'TestLibrary.dll';

procedure pushInt32(x: Integer); cdecl; external 'TestLibrary.dll';
function popInt32(): Integer; cdecl; external 'TestLibrary.dll';

procedure pushString(str: PChar; length: NativeUInt); cdecl; external 'TestLibrary.dll';
procedure popString(pStr: PPChar; pLength: PNativeUInt); cdecl; external 'TestLibrary.dll';

procedure pushClientFunc(id: Integer); cdecl; external 'TestLibrary.dll';

procedure pushInt32Array(values: PInteger; count: NativeUInt); cdecl; external 'TestLibrary.dll';
procedure popInt32Array(values: PPtrInt; count: PNativeUInt); cdecl; external 'TestLibrary.dll';

function popServerFunc(): Integer; cdecl; external 'TestLibrary.dll';

procedure execServerFunc(id: Integer); cdecl; external 'TestLibrary.dll';
function execServerFuncWithExceptions(id: Integer): TExceptionRef; cdecl; external 'TestLibrary.dll';

procedure invokeInterfaceMethod(method: TInterfaceMethodRef; serverID: Integer); cdecl; external 'TestLibrary.dll';
function invokeInterfaceMethodWithExceptions(method: TInterfaceMethodRef; serverID: Integer): TExceptionRef; cdecl; external 'TestLibrary.dll';
procedure pushInstance(id: Integer; isClientId: Boolean; inSafetyArea: Boolean); cdecl; external 'TestLibrary.dll';
function popInstance(isClientId: PBoolean; inSafetyArea: PBoolean): Integer; cdecl; external 'TestLibrary.dll';
procedure pushNull(); cdecl; external 'TestLibrary.dll';
procedure releaseServerResource(id: Integer); cdecl; external 'TestLibrary.dll';
procedure clearServerSafetyArea(); cdecl; external 'TestLibrary.dll';

implementation

end.

