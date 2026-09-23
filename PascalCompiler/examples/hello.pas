program Hello;
var
  nombre: string;
  x, y, suma: integer;
begin
  nombre := 'Mundo';
  writeln('Hola, ', nombre, '!');

  x := 5;
  y := 7;
  suma := x + y;
  writeln('La suma de ', x, ' y ', y, ' es ', suma);

  if suma > 10 then
    writeln('La suma es mayor que 10')
  else
    writeln('La suma es 10 o menos');
end.
