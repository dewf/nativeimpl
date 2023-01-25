unit ClientFuncValue;

{$mode ObjFPC}{$H+}
{$modeswitch advancedrecords}

// suppress fgl inline warning shit
{$warn 3123 off} // "inherited" not yet supported inside inline procedure/function
{$warn 3124 off} // inlining disabled
{$warn 6058 off} // ... marked as inline is not inlined

interface

uses
  Classes, SysUtils, fgl, ClientResource;

type
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

implementation

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


end.

