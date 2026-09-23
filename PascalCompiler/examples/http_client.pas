program HttpClient;

begin
  // GET simple contra un endpoint REST/JSON.
  if HttpReqGet('http://localhost:8080/hello') then
  begin
    writeln('GET /hello -> status ', HttpRespStatus());
    writeln('Body: ', HttpRespBody());
  end
  else
    writeln('GET /hello fallo, status ', HttpRespStatus());

  // GET con query string.
  if HttpReqGet('http://localhost:8080/saludo?nombre=Cliente') then
    writeln('GET /saludo -> ', HttpRespBody());

  // POST con body JSON (mismo patron que usaria para pegarle a cualquier
  // servicio REST; para SOAP seria HttpReqSetContentType('text/xml') +
  // HttpReqSetHeader('SOAPAction', '...') + un body XML en vez de JSON).
  HttpReqSetHeader('X-Custom', 'desde-pascal');
  if HttpReqPost('http://localhost:8080/echo', '{"nombre":"Diego","edad":22}') then
  begin
    writeln('POST /echo -> status ', HttpRespStatus());
    writeln('Body: ', HttpRespBody());
  end;

  // Ruta inexistente: status de error, sin explotar.
  if not HttpReqGet('http://localhost:8080/noexiste') then
    writeln('GET /noexiste -> status ', HttpRespStatus(), ' (esperado: fallo)');
end.
