program ApiAuth;

var
  passwordHash: string;
  secret: string;

begin
  secret := 'clave-secreta-super-segura';
  passwordHash := HashPassword('correcta123');
  writeln('Hash almacenado: ', passwordHash);

  writeln('Escuchando en http://localhost:8080 ...');
  HttpStart(8080);
  while HttpWait() do
  begin
    HttpSetHeader('Content-Type', 'application/json');

    if (HttpMethod() = 'POST') and (HttpPath() = '/login') then
    begin
      if VerifyPassword(JsonGetString(HttpBody(), 'password'), passwordHash) then
      begin
        HttpSetStatus(200);
        HttpWrite('{"token": ' + JsonEscape(
          JwtSign('{"user":"' + JsonGetString(HttpBody(), 'user') +
                  '","exp":' + IntToStr(JwtNow() + 3600) + '}', secret)
        ) + '}');
      end
      else
      begin
        HttpSetStatus(401);
        HttpWrite('{"error": "credenciales invalidas"}');
      end;
    end
    else if HttpPath() = '/perfil' then
    begin
      if JwtVerify(HttpBearerToken(), secret) then
      begin
        HttpSetStatus(200);
        HttpWrite('{"usuario": ' + JsonEscape(JsonGetString(JwtPayload(HttpBearerToken()), 'user')) + '}');
      end
      else
      begin
        HttpSetStatus(401);
        HttpWrite('{"error": "token invalido o ausente"}');
      end;
    end
    else
    begin
      HttpSetStatus(404);
      HttpWrite('{"error": "not found"}');
    end;
    HttpEnd();
  end;
end.
