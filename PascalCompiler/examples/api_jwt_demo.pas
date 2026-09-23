program ApiJwtDemo;

function UserExists(email: string): boolean;
begin
  DbQuery('SELECT id FROM usuarios WHERE email = ''' + email + '''');
  Result := DbNext();
end;

procedure HandleRegister(secret: string);
var
  body, email, password, hash: string;
begin
  body := HttpBody();
  email := JsonGetString(body, 'email');
  password := JsonGetString(body, 'password');

  if (email = '') or (password = '') then
  begin
    HttpSetStatus(400);
    HttpWrite('{"error":"email y password son requeridos"}');
  end
  else if UserExists(email) then
  begin
    HttpSetStatus(409);
    HttpWrite('{"error":"el usuario ya existe"}');
  end
  else
  begin
    hash := HashPassword(password);
    DbExecute('INSERT INTO usuarios (email, passwordHash) VALUES (''' + email + ''', ''' + hash + ''')');
    HttpSetStatus(201);
    HttpWrite('{"mensaje":"usuario creado"}');
  end;
end;

procedure HandleLogin(secret: string);
var
  body, email, password, storedHash, token: string;
  found: boolean;
begin
  body := HttpBody();
  email := JsonGetString(body, 'email');
  password := JsonGetString(body, 'password');

  DbQuery('SELECT passwordHash FROM usuarios WHERE email = ''' + email + '''');
  found := DbNext();

  if not found then
  begin
    HttpSetStatus(401);
    HttpWrite('{"error":"credenciales invalidas"}');
  end
  else
  begin
    storedHash := DbGetString('passwordHash');
    if VerifyPassword(password, storedHash) then
    begin
      token := JwtSign('{"email":' + JsonEscape(email) + ',"exp":' + IntToStr(JwtNow() + 3600) + '}', secret);
      HttpSetStatus(200);
      HttpWrite('{"token":' + JsonEscape(token) + '}');
    end
    else
    begin
      HttpSetStatus(401);
      HttpWrite('{"error":"credenciales invalidas"}');
    end;
  end;
end;

procedure HandleProfile(secret: string);
var
  token, payload, email: string;
begin
  token := HttpBearerToken();
  if (token = '') or (not JwtVerify(token, secret)) then
  begin
    HttpSetStatus(401);
    HttpWrite('{"error":"token invalido o ausente"}');
  end
  else
  begin
    payload := JwtPayload(token);
    email := JsonGetString(payload, 'email');
    HttpSetStatus(200);
    HttpWrite('{"email":' + JsonEscape(email) + ',"mensaje":"acceso concedido"}');
  end;
end;

var
  secret: string;
begin
  secret := 'clave-secreta-super-segura-cambiar-en-produccion';

  DbConnect('usuarios_demo.db');
  DbExecute('CREATE TABLE IF NOT EXISTS usuarios (id INTEGER PRIMARY KEY AUTOINCREMENT, email TEXT UNIQUE, passwordHash TEXT)');

  writeln('API JWT demo escuchando en http://localhost:8080 ...');
  writeln('  POST /register  {"email":"...", "password":"..."}');
  writeln('  POST /login     {"email":"...", "password":"..."}  -> devuelve token');
  writeln('  GET  /profile   (Authorization: Bearer <token>)');

  HttpStart(8080);
  while HttpWait() do
  begin
    HttpSetHeader('Content-Type', 'application/json');

    if (HttpMethod() = 'POST') and (HttpPath() = '/register') then
      HandleRegister(secret)
    else if (HttpMethod() = 'POST') and (HttpPath() = '/login') then
      HandleLogin(secret)
    else if (HttpMethod() = 'GET') and (HttpPath() = '/profile') then
      HandleProfile(secret)
    else
    begin
      HttpSetStatus(404);
      HttpWrite('{"error":"not found"}');
    end;

    HttpEnd();
  end;
end.
