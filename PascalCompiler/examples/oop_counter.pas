program OopCounter;

type
  TCounter = class
    value: integer;
    procedure Increment;
    procedure IncrementBy(n: integer);
    function GetValue: integer;
  end;

procedure TCounter.Increment;
begin
  value := value + 1;
end;

procedure TCounter.IncrementBy(n: integer);
begin
  value := value + n;
end;

function TCounter.GetValue: integer;
begin
  Result := value;
end;

var
  c1, c2: TCounter;
begin
  c1 := TCounter.Create();
  c1.Increment();
  c1.Increment();
  c1.IncrementBy(10);
  writeln('c1: ', c1.GetValue());

  c2 := TCounter.Create();
  c2.IncrementBy(5);
  writeln('c2: ', c2.GetValue());

  writeln('c1 sigue en: ', c1.GetValue());
end.
