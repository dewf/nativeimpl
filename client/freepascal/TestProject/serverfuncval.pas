unit ServerFuncVal;

{$mode ObjFPC}{$H+}

interface

uses
  Classes, SysUtils, ServerResource, NativeMethods;

type
  { TServerFuncVal }
  TServerFuncVal = class(TServerResource)
    public
      procedure Invoke();
  end;

  { TServerFuncWrapperBase }
  generic TServerFuncWrapperBase<TInterface,TWrapper> = class(TInterfacedObject)
  private
    _f: TServerFuncVal;
  public
    class function Pop(): TInterface;
    constructor Create(f: TServerFuncVal);
    destructor Destroy; override;
  end;

implementation

{ TServerFuncVal }

procedure TServerFuncVal.Invoke;
begin
  NativeMethods.execServerFunc(_id);
end;

{ TServerFuncWrapperBase }

class function TServerFuncWrapperBase.Pop: TInterface;
var
  f: TServerFuncVal;
  id: Integer;
begin
  id := NativeMethods.popServerFunc();
  f := TServerFuncVal.Create(id);
  result := TWrapper.Create(f);
end;

constructor TServerFuncWrapperBase.Create(f: TServerFuncVal);
begin
  _f := f;
end;

destructor TServerFuncWrapperBase.Destroy;
begin
  _f.Free;
  inherited Destroy;
end;


end.


