program MatrizYRecords;

type
  TPunto = record
    x: integer;
    y: integer;
  end;

function CrearPunto(px, py: integer): TPunto;
begin
  Result.x := px;
  Result.y := py;
end;

function SumarPuntos(a: TPunto; b: TPunto): TPunto;
begin
  Result.x := a.x + b.x;
  Result.y := a.y + b.y;
end;

function DistanciaCuadrada(p: TPunto): integer;
begin
  Result := p.x * p.x + p.y * p.y;
end;

var
  matriz: array[1..3, 1..3] of integer;
  i, j, suma: integer;
  p1, p2, p3: TPunto;
begin
  for i := 1 to 3 do
    for j := 1 to 3 do
      matriz[i, j] := i * 10 + j;

  writeln('Matriz:');
  for i := 1 to 3 do
  begin
    for j := 1 to 3 do
      write(matriz[i, j], ' ');
    writeln;
  end;

  suma := 0;
  for i := 1 to 3 do
    for j := 1 to 3 do
      suma := suma + matriz[i, j];
  writeln('Suma total: ', suma);

  p1 := CrearPunto(3, 4);
  p2 := CrearPunto(1, 2);
  p3 := SumarPuntos(p1, p2);

  writeln('p1 = (', p1.x, ', ', p1.y, ')');
  writeln('p2 = (', p2.x, ', ', p2.y, ')');
  writeln('p1 + p2 = (', p3.x, ', ', p3.y, ')');
  writeln('|p1|^2 = ', DistanciaCuadrada(p1));
end.
