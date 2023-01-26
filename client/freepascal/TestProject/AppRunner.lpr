program AppRunner;

{$mode ObjFPC}{$H+}

uses TestLibrary, SysUtils;

type
  { MyIntFunc }
  MyIntFunc = class(TInterfacedObject, ISimpleIntFunc)
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

// functions ============================================

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

// everything in a proc so that we can test interface method counting
// (if done at root application level, stuff doesn't get freed)
procedure TestEverything();
var
  sum: Integer;
  appendedStr: String;
  valuesToSum: Array of Integer = (1, 2, 3, 4, 5, 6, 7, 8, 9, 10); // 55
  valuesToSum2: Array[0..19] of Integer; // 190
  summed: Integer;
  i: Integer;
  intFunc: ISimpleIntFunc;
  intFuncResult: Integer;
  arrayFuncResult: Integer;
  serverFunc: ISimpleIntFunc;

  thing: ICoolThing;
begin
  //sum := TestLibrary.Sum(101, 202, 303);
  //writeln('sum is: ' + IntToStr(sum));
  //
  //appendedStr := TestLibrary.AppendToString('@@WOOT DOOT@@');
  //writeln('appended string is: ' + appendedStr);
  //
  //summed := TestLibrary.ArraySum(valuesToSum);
  //writeln('summed values: ' + IntToStr(summed));
  //
  //for i:=0 to 19 do
  //begin
  //  valuesToSum2[i] := i;
  //end;
  //summed := TestLibrary.ArraySum(valuesToSum2);
  //writeln('summed2: ' + IntToStr(summed));
  //
  //intFunc := MyIntFunc.Create();
  //
  //TestLibrary.StashFunc(intFunc);
  //
  //intFuncResult := TestLibrary.ApplyIntFunc(intFunc, 123);
  //writeln('applyIntFunc(123) = ' + IntToStr(intFuncResult));
  //
  //TestLibrary.ClearedStashedFunc();
  //intFunc := nil;
  //
  //arrayFuncResult := TestLibrary.ApplyArrayFunc(MyArrayFunc.Create(), 100);
  //writeln('applyArrayFunc(f, 100) = ' + IntToStr(arrayFuncResult));

  //serverFunc := TestLibrary.MakeIntFunc();
  //i := serverFunc.Apply(981);
  //writeln('serverFunc(981) = ' + IntToStr(i));
  //
  //i := TestLibrary.ApplyIntFunc(serverFunc, 567);
  //writeln('applyIntFunc(serverFunc, 567) = ' + IntToStr(i));

  thing := TestLibrary.MakeThing();
  i := thing.doSomething(9876);
  writeln('thing.doSomething(9876) = ' + IntToStr(i));
end;

// app proper ====================
begin
  writeln('-------- app start -------------');
  if not TestLibrary.Init() then
  begin
        writeln('init failed :(');
        exit();
  end;

  // separated into a function so we can test refcounting
  // at root program level stuff never got freed ...
  TestEverything();

  TestLibrary.Shutdown();
  writeln('(enter to exit)');
  readln();
end.
