unit ServerObject;

{$mode ObjFPC}{$H+}

interface

uses
  Classes, SysUtils, ServerResource, NativeImplClient;

type
  { TServerObject }
  TServerObject = class(TServerResource)
    public
      procedure InvokeMethod(method: TInterfaceMethodHandle);
  end;

implementation

{ TServerObject }
procedure TServerObject.InvokeMethod(method: TInterfaceMethodHandle);
begin
  NativeImplClient.InvokeInterfaceMethod(method, _id);
  //NativeMethods.invokeInterfaceMethod(method, _id);
end;

end.

