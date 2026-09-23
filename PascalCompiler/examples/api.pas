program MiApi;
begin
  writeln('Escuchando en http://localhost:8080 ...');
  HttpStart(8080);
  while HttpWait() do
  begin
    HttpSetHeader('Content-Type', 'application/json');

    if HttpPath() = '/hello' then
    begin
      HttpSetStatus(200);
      HttpWrite('{"mensaje": "Hola desde Pascal", "metodo": "' + HttpMethod() + '"}');
    end
    else if HttpPath() = '/saludo' then
    begin
      HttpSetStatus(200);
      HttpWrite('{"saludo": ' + JsonEscape('Hola, ' + HttpQuery('nombre') + '!') + '}');
    end
    else if HttpMatch('/users/:id') then
    begin
      HttpSetStatus(200);
      HttpWrite('{"userId": ' + JsonEscape(HttpParam('id')) + '}');
    end
    else if (HttpMethod() = 'POST') and (HttpPath() = '/echo') then
    begin
      HttpSetStatus(200);
      HttpWrite('{"recibido": ' + JsonEscape(JsonGetString(HttpBody(), 'nombre')) +
                ', "edad": ' + IntToStr(JsonGetInt(HttpBody(), 'edad')) + '}');
    end
    else
    begin
      HttpSetStatus(404);
      HttpWrite('{"error": "not found"}');
    end;
    HttpEnd();
  end;
end.
