program OopPolymorphism;

type
  TAnimal = class
    name: string;
    procedure SetName(n: string);
    procedure Speak; virtual;
  end;

  TDog = class(TAnimal)
    procedure Speak; override;
  end;

  TCat = class(TAnimal)
    procedure Speak; override;
  end;

procedure TAnimal.SetName(n: string);
begin
  name := n;
end;

procedure TAnimal.Speak;
begin
  writeln(name, ' hace un sonido generico.');
end;

procedure TDog.Speak;
begin
  inherited Speak; // primero el comportamiento base, despues el propio
  writeln(name, ' dice: Guau!');
end;

procedure TCat.Speak;
begin
  writeln(name, ' dice: Miau!');
end;

var
  a1, a2: TAnimal;
  d: TDog;
  c: TCat;
begin
  d := TDog.Create();
  d.SetName('Firulais');

  c := TCat.Create();
  c.SetName('Michi');

  // a1/a2 son variables de tipo TAnimal, pero contienen instancias de TDog/TCat:
  // Speak() despacha polimorficamente a la version correcta en tiempo de ejecucion,
  // no a la de TAnimal (que es el tipo *declarado* de a1/a2).
  a1 := d;
  a2 := c;

  a1.Speak();
  a2.Speak();
end.
