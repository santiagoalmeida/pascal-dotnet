program OopInheritance;

type
  TAnimal = class
    name: string;
    procedure SetName(n: string);
    function Describe: string;
  end;

  TDog = class(TAnimal)
    breed: string;
    procedure SetBreed(b: string);
    function FullDescription: string;
  end;

procedure TAnimal.SetName(n: string);
begin
  name := n;
end;

function TAnimal.Describe: string;
begin
  Result := 'Animal: ' + name;
end;

procedure TDog.SetBreed(b: string);
begin
  breed := b;
end;

function TDog.FullDescription: string;
begin
  // Self.Describe llama a un método heredado de TAnimal desde dentro de TDog.
  Result := Self.Describe() + ' (raza: ' + breed + ')';
end;

var
  d: TDog;
begin
  d := TDog.Create();
  d.SetName('Firulais');       // método heredado, llamado desde afuera
  d.SetBreed('Labrador');
  writeln(d.FullDescription());
  writeln(d.Describe());       // método heredado, llamado directo desde afuera
end.
