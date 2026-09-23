program OopRectangle;

type
  TRectangle = class
    width: integer;
    height: integer;
    function Area: integer;
    function Perimeter: integer;
    function IsSquare: boolean;
    procedure Scale(factor: integer);
    procedure Describe;
  end;

function TRectangle.Area: integer;
begin
  Result := width * height;
end;

function TRectangle.Perimeter: integer;
begin
  Result := 2 * (width + height);
end;

function TRectangle.IsSquare: boolean;
begin
  Result := width = height;
end;

procedure TRectangle.Scale(factor: integer);
begin
  width := width * factor;
  height := height * factor;
end;

procedure TRectangle.Describe;
begin
  writeln('Rectangulo ', width, 'x', height,
          ' area=', Self.Area(), ' perimetro=', Self.Perimeter());
  if Self.IsSquare() then
    writeln('  (es un cuadrado)')
  else
    writeln('  (no es un cuadrado)');
end;

var
  r1, r2: TRectangle;
begin
  r1 := TRectangle.Create();
  r1.width := 4;
  r1.height := 6;
  r1.Describe();

  r2 := TRectangle.Create();
  r2.width := 5;
  r2.height := 5;
  r2.Describe();

  writeln('Despues de escalar r1 x2:');
  r1.Scale(2);
  r1.Describe();
end.
