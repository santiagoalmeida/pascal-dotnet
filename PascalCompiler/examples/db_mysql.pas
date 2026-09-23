program DbMySql;

var
  total: integer;
begin
  DbConnect('mysql:Server=localhost;Port=3307;Uid=root;Pwd=pascal123;Database=pascaldb');
  DbExecute('DROP TABLE IF EXISTS usuarios');
  DbExecute('CREATE TABLE usuarios (id INT AUTO_INCREMENT PRIMARY KEY, nombre VARCHAR(50), edad INT)');

  DbExecute('INSERT INTO usuarios (nombre, edad) VALUES (''Ana'', 30)');
  DbExecute('INSERT INTO usuarios (nombre, edad) VALUES (''Beto'', 25)');
  DbExecute('INSERT INTO usuarios (nombre, edad) VALUES (''Carla'', 40)');

  writeln('Usuarios (MySQL):');
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
