unit ClientResource;

{$mode ObjFPC}{$H+}

// suppress fgl inline warning shit
{$warn 3123 off} // "inherited" not yet supported inside inline procedure/function
{$warn 3124 off} // inlining disabled
{$warn 6058 off} // ... marked as inline is not inlined

interface

uses
  Classes, SysUtils, fgl;

type
  { TClientResource }
  TClientResource = class
    type
      TResourceIdMap = specialize TFPGMap<Integer,TClientResource>;
    private
      RefCount: Integer;
    private
      _nextId: Integer; static;
      _resourceMap: TResourceIdMap; static;
      class constructor Create;
    public
      _id: Integer;
      class function GetByID(id: Integer): TClientResource;
      constructor Create;
      procedure AddRef();
      procedure Release();
  end;

implementation

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

