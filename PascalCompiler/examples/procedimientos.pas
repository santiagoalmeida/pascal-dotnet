program Procedimientos;

procedure Saludar(nombre: string; veces: integer);
var
  i: integer;
begin
  for i := 1 to veces do
    writeln('Hola, ', nombre, '! (', i, ')');
end;

function Factorial(n: integer): integer;
begin
  if n <= 1 then
    Result := 1
  else
    Result := n * Factorial(n - 1);
end;

var
  k: integer;
begin
  Saludar('Santiago', 3);
  for k := 1 to 6 do
    writeln(k, '! = ', Factorial(k));
end.
