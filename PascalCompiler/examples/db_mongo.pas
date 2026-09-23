program DbMongo;

var
  doc: string;
  total: integer;
begin
  MongoConnect('mongodb://localhost:27018', 'pascaldb');

  MongoDelete('usuarios', '{}');
  MongoInsert('usuarios', '{"nombre":"Ana","edad":30}');
  MongoInsert('usuarios', '{"nombre":"Beto","edad":25}');
  MongoInsert('usuarios', '{"nombre":"Carla","edad":40}');

  writeln('Usuarios (Mongo): ', MongoCount('usuarios', '{}'));
  MongoFind('usuarios', '{}');
  while MongoNext() do
  begin
    doc := MongoGetDocument();
    writeln('  ', JsonGetString(doc, 'nombre'), ' (', JsonGetInt(doc, 'edad'), ' anios)');
  end;

  MongoUpdate('usuarios', '{"nombre":"Beto"}', '{"$set":{"edad":26}}');
  writeln('Mayores de 27:');
  MongoFind('usuarios', '{"edad":{"$gt":27}}');
  while MongoNext() do
  begin
    doc := MongoGetDocument();
    writeln('  ', JsonGetString(doc, 'nombre'), ' (', JsonGetInt(doc, 'edad'), ' anios)');
  end;
end.
