program RecordsYStrings;

type
  TPersona = record
    nombre: string;
    edad: integer;
  end;

function DescribirPersona(nombre: string; edad: integer): string;
begin
  Result := nombre + ' tiene ' + IntToStr(edad) + ' anios';
end;

var
  p1, p2: TPersona;
  frase: string;
  n: integer;
begin
  p1.nombre := 'Ana';
  p1.edad := 30;

  writeln(DescribirPersona(p1.nombre, p1.edad));

  p2 := p1;
  p2.edad := 31;
  writeln('p1.edad=', p1.edad, ' p2.edad=', p2.edad, ' (comparten referencia)');

  frase := '  Hola Mundo Pascal  ';
  writeln('Longitud: ', Length(frase));
  writeln('Trim: [', Trim(frase), ']');
  writeln('Mayusculas: ', UpperCase(Trim(frase)));
  writeln('Minusculas: ', LowerCase(Trim(frase)));
  writeln('Copy(2,4): [', Copy(Trim(frase), 1, 4), ']');

  n := StrToInt('123') + StrToInt('7');
  writeln('StrToInt suma: ', n, ' -> IntToStr: ', IntToStr(n));
end.
