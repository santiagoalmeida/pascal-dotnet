program OopTask;

type
  TTask = class
    title: string;
    done: boolean;
    procedure Complete;
    procedure Describe;
  end;

procedure TTask.Complete;
begin
  done := true;
end;

procedure TTask.Describe;
begin
  if done then
    writeln('[x] ', title)
  else
    writeln('[ ] ', title);
end;

var
  t: TTask;
begin
  t := TTask.Create();
  t.title := 'Escribir ejemplos de OOP';
  t.Describe();

  t.Complete();
  t.Describe();

  writeln('--- reciclando la variable con una tarea nueva ---');
  t := TTask.Create();
  t.title := 'Revisar PRs';
  t.Describe(); // debe volver a arrancar en [ ], sin arrastrar el 'done' anterior
end.
