program DbOracle;

var
  total: integer;
begin
  DbConnect('oracle:User Id=system;Password=pascal123;Data Source=localhost:1522/FREEPDB1');
  DbExecute('BEGIN EXECUTE IMMEDIATE ''DROP TABLE usuarios''; EXCEPTION WHEN OTHERS THEN NULL; END;');
  DbExecute('CREATE TABLE usuarios (id NUMBER GENERATED ALWAYS AS IDENTITY PRIMARY KEY, nombre VARCHAR2(50), edad NUMBER)');

  DbExecute('INSERT INTO usuarios (nombre, edad) VALUES (''Ana'', 30)');
  DbExecute('INSERT INTO usuarios (nombre, edad) VALUES (''Beto'', 25)');
  DbExecute('INSERT INTO usuarios (nombre, edad) VALUES (''Carla'', 40)');

  writeln('Usuarios (Oracle):');
  DbQuery('SELECT id, nombre, edad FROM usuarios ORDER BY edad');
  while DbNext() do
    writeln('  #', DbGetInt('id'), ' ', DbGetString('nombre'), ' (', DbGetInt('edad'), ' anios)');

  total := 0;
  DbQuery('SELECT edad FROM usuarios');
  while DbNext() do
    total := total + DbGetInt('edad');
  writeln('Suma de edades: ', total);

  DbClose();
end.
