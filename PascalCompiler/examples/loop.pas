program ContadorPrimos;
var
  n, i, divisor, esPrimo: integer;
begin
  n := 1;
  while n <= 30 do
  begin
    if n < 2 then
      esPrimo := 0
    else
      esPrimo := 1;

    divisor := 2;
    while (divisor * divisor <= n) and (esPrimo = 1) do
    begin
      if n mod divisor = 0 then
        esPrimo := 0;
      divisor := divisor + 1;
    end;

    if esPrimo = 1 then
      writeln(n, ' es primo');

    n := n + 1;
  end;
end.
