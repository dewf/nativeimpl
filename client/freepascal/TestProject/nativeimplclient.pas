
unit NativeImplClient;

{$mode ObjFPC}{$H+}
//{$interfaces corba} // no COM interfaces
{$modeswitch advancedrecords}

// suppress fgl inline warning shit
{$warn 3123 off} // "inherited" not yet supported inside inline procedure/function
{$warn 3124 off} // inlining disabled
{$warn 6058 off} // ... marked as inline is not inlined

interface

uses
  Classes, SysUtils, NativeMethods, fgl;

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

  { TClientResource }
  TClientResource = class
    type
      TResourceIdMap = specialize TFPGMap<Integer,TClientResource>;
    private
      _id: Integer;
      RefCount: Integer;
      procedure AddRef();
      procedure Release();
    private
      _nextId: Integer; static;
      _resourceMap: TResourceIdMap; static;
      class constructor Create;
      class function GetByID(id: Integer): TClientResource;
    public
      constructor Create;
  end;

  TWrapperFunc = procedure(f: IUnknown);

  { TClosureInfo }
  TClosureInfo = record
    wrapper: TWrapperFunc;
    f: IUnknown;
    class operator <(a, b: TClosureInfo): boolean;
    class operator >(a, b: TClosureInfo): boolean;
  end;

  { TClientFuncValue }
  TClientFuncValue = class (TClientResource)
    type
      TExistingMap = specialize TFPGMap<TClosureInfo,TClientFuncValue>;
    private
      existingMap: TExistingMap; static;
      _closure: TClosureInfo;
    public
      class constructor Create; // create map
      class function GetByID(id: Integer): TClientFuncValue; inline; // for fetching on ClientFuncExec
      class function GetOrCreate(closure: TClosureInfo): TClientFuncValue;
      procedure Apply();
      destructor Destroy; override; // remove self from map
  end;

  TIntArray = Array of Integer;

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
function PopInt32Array(): TIntArray;

procedure PushFuncValue(wrapper: TWrapperFunc; f: IUnknown);

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

{ TClosureInfo }

class operator TClosureInfo.<(a, b: TClosureInfo): boolean;
begin
  if Pointer(a.wrapper) < Pointer(b.wrapper) then
    result := true
  else
    if Pointer(a.wrapper) = Pointer(b.wrapper) then
      result := Pointer(a.f) < Pointer(b.f)
    else
      result := false
end;

class operator TClosureInfo.>(a, b: TClosureInfo): boolean;
begin
  if Pointer(a.wrapper) > Pointer(b.wrapper) then
    result := true
  else
    if Pointer(a.wrapper) = Pointer(b.wrapper) then
      result := Pointer(a.f) > Pointer(b.f)
    else
      result := false
end;

{ TClientFuncValue }

class constructor TClientFuncValue.Create;
begin
  existingMap := TExistingMap.Create;
end;

class function TClientFuncValue.GetByID(id: Integer): TClientFuncValue; inline;
begin
  result := TClientFuncValue(TClientResource.GetByID(id));
end;

class function TClientFuncValue.GetOrCreate(closure: TClosureInfo): TClientFuncValue;
var
  funcValue: TClientFuncValue;
begin
  // check the map for existing
  // if not there, create a new one
  if existingMap.TryGetData(closure, funcValue) then
  begin
    // already exists
    writeln('TClientFuncValue.GetOrCreate(): already exists, nice!');
    // but we need to add a reference, since it's going across again (on return from this func)
    funcValue.AddRef();
  end else
  begin
    // need to create anew
    funcValue := TClientFuncValue.Create;  // super ctor, I think
    funcValue._closure := closure;
    existingMap.Add(closure, funcValue);   // to find it in the future
  end;
  result := funcValue;
end;

procedure TClientFuncValue.Apply;
begin
  _closure.wrapper(_closure.f);
end;

destructor TClientFuncValue.Destroy;
begin
  writeln('TClientFuncValue.Destroy - removing from map');
  // remove self from 'existing' map
  existingMap.Remove(_closure); // closure is key
end;

// end callbacks ==============================================================


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

function PopInt32Array: TIntArray;
var
  values: PInteger;
  count: NativeUInt;
  arr: array of Integer;
  i: Integer;
begin
  NativeMethods.popInt32Array(@values, @count);
  arr := nil;
  setLength(arr, count);
  // TODO: how to efficiently (and correctly!) copy?
  for i := 0 to count do
      arr[i] := values[i];
  result := arr;
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

{ TClientResource }

constructor TClientResource.Create;
begin
  RefCount := 1;
  _id := _nextId;
  Inc(_nextId);
  // add self to table
  _resourceMap.Add(_id, self);
end;

procedure TClientResource.AddRef;
begin
  Inc(RefCount);
end;

procedure TClientResource.Release;
begin
  Dec(RefCount);
  if RefCount = 0 then
  begin
    writeln('freepascal: TClientResource releasing');
    // need to remove from table + self destruct
    _resourceMap.Remove(_id);
    Destroy;
  end;
end;

class constructor TClientResource.Create;
begin
  _nextId := 1;
  _resourceMap := TResourceIdMap.Create();
end;

class function TClientResource.GetByID(id: Integer): TClientResource;
var
  outItem: TClientResource;
begin
  if _resourceMap.TryGetData(id, outItem) then
  begin
    // outItem already set
  end else
  begin
    writeln('TClientResource.GetById() - id not found!');
    outItem := nil;
  end;
  result := outItem;
end;

end.
