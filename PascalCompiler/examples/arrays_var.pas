program ArraysYVar;

procedure Swap(var a: integer; var b: integer);
var
  temp: integer;
begin
  temp := a;
  a := b;
  b := temp;
end;

procedure BubbleSort(var arr: array[1..8] of integer);
var
  i, j: integer;
begin
  for i := 1 to 7 do
    for j := 1 to 8 - i do
      if arr[j] > arr[j + 1] then
        Swap(arr[j], arr[j + 1]);
end;

var
  numeros: array[1..8] of integer;
  x, y, k: integer;
begin
  x := 10;
  y := 20;
  writeln('Antes de Swap: x=', x, ' y=', y);
  Swap(x, y);
  writeln('Despues de Swap: x=', x, ' y=', y);

  numeros[1] := 5;
  numeros[2] := 2;
  numeros[3] := 9;
  numeros[4] := 1;
  numeros[5] := 7;
  numeros[6] := 3;
  numeros[7] := 8;
  numeros[8] := 4;

  BubbleSort(numeros);

  write('Array ordenado: ');
  for k := 1 to 8 do
    write(numeros[k], ' ');
  writeln;
end.
