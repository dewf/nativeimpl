unit ServerResource;

{$mode ObjFPC}{$H+}

interface

uses
  Classes, SysUtils, NativeMethods;

type
  { TServerResource }
  TServerResource = class
    protected
      _id: Integer;
    public
      constructor Create(id: Integer);
      destructor Destroy; override;
  end;

implementation

{ TServerResource }

constructor TServerResource.Create(id: Integer);
begin
  _id := id;
end;

destructor TServerResource.Destroy;
begin
  NativeMethods.releaseServerResource(_id);
  inherited Destroy;
end;

end.

