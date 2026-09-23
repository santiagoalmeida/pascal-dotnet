program DbSqlServer;

var
  total: integer;
begin
  DbConnect('sqlserver:Server=localhost,1434;User Id=sa;Password=Pascal123!;TrustServerCertificate=True');
  DbExecute('IF OBJECT_ID(''usuarios'', ''U'') IS NOT NULL DROP TABLE usuarios');
  DbExecute('CREATE TABLE usuarios (id INT IDENTITY PRIMARY KEY, nombre NVARCHAR(50), edad INT)');

  DbExecute('INSERT INTO usuarios (nombre, edad) VALUES (''Ana'', 30)');
  DbExecute('INSERT INTO usuarios (nombre, edad) VALUES (''Beto'', 25)');
  DbExecute('INSERT INTO usuarios (nombre, edad) VALUES (''Carla'', 40)');

  writeln('Usuarios (SQL Server):');
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
