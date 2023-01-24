program AppRunner;

{$mode ObjFPC}{$H+}

uses TestLibrary, SysUtils;

type
  { MyIntFunc }
  MyIntFunc = class(TInterfacedObject, ISomeIntFunc)
    public
      function Apply(x: Integer): Integer;
      destructor Destroy; override;
  end;

  { MyArrayFunc }

  MyArrayFunc = class(TInterfacedObject, IArrayFunc)
    public
      function Apply(items: array of Integer): Integer;
      destructor Destroy; override;
  end;

var
  sum: Integer;
  appendedStr: String;
  valuesToSum: Array of Integer = (1, 2, 3, 4, 5, 6, 7, 8, 9, 10); // 55
  valuesToSum2: Array[0..19] of Integer; // 190
  summed: Integer;
  i: Integer;
  //intFunc: ISomeIntFunc;
  intFuncResult: Integer;
  arrayFuncResult: Integer;

{ MyArrayFunc }

function MyArrayFunc.Apply(items: array of Integer): Integer;
var
  i, sum: Integer;
begin
  sum := 0;
  for i := 0 to length(items) - 1 do
  begin
    sum := sum + items[i];
  end;
  result := sum;
end;

destructor MyArrayFunc.Destroy;
begin
  inherited Destroy;
  writeln('MyArrayFunc.Destroy!');
end;

{ MyIntFunc }
function MyIntFunc.Apply(x: Integer): Integer;
begin
  result := x * 1001;
end;

destructor MyIntFunc.Destroy;
begin
  inherited Destroy;
  writeln('MyIntFunc.Destroy!');
end;

begin
  writeln('-------- app start -------------');
    
  if not TestLibrary.Init() then
  begin
        writeln('init failed :(');
        exit();
  end;

  sum := TestLibrary.Sum(101, 202, 303);
  writeln('sum is: ' + IntToStr(sum));

  appendedStr := TestLibrary.AppendToString('@@WOOT DOOT@@');
  writeln('appended string is: ' + appendedStr);

  summed := TestLibrary.ArraySum(valuesToSum);
  writeln('summed values: ' + IntToStr(summed));

  for i:=0 to 19 do
  begin
    valuesToSum2[i] := i;
  end;
  summed := TestLibrary.ArraySum(valuesToSum2);
  writeln('summed2: ' + IntToStr(summed));

  intFuncResult := TestLibrary.ApplyIntFunc(MyIntFunc.Create(), 123);
  writeln('applyIntFunc(123) = ' + IntToStr(intFuncResult));

  arrayFuncResult := TestLibrary.ApplyArrayFunc(MyArrayFunc.Create(), 100);
  writeln('applyArrayFunc(f, 100) = ' + IntToStr(arrayFuncResult));

  TestLibrary.Shutdown();
  writeln('(enter to exit)');
  readln();
end.
