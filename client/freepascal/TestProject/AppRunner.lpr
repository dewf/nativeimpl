program AppRunner;

uses TestLibrary, SysUtils;

var
  sum: Integer;
  appendedStr: String;
  appended2: String;
  valuesToSum: Array of Integer = (1, 2, 3, 4, 5, 6, 7, 8, 9, 10); // 55
  valuesToSum2: Array[0..19] of Integer; // 190
  summed: Integer;
  i: Integer;

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

  appended2 := TestLibrary.AppendToString('... noice! ...');
  writeln('appended #2 is: ' + appended2);

  summed := TestLibrary.ArraySum(valuesToSum);
  writeln('summed values: ' + IntToStr(summed));

  for i:=0 to 19 do
  begin
    valuesToSum2[i] := i;
  end;
  summed := TestLibrary.ArraySum(valuesToSum2);
  writeln('summed2: ' + IntToStr(summed));


  TestLibrary.Shutdown();
  writeln('(enter to exit)');
  readln();
end.
