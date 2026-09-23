program OopCtorInjection;

type
  TLogger = class
    prefix: string;
    constructor Create(p: string);
    procedure Log(msg: string);
  end;

  TService = class
    logger: TLogger;
    name: string;
    constructor Create(l: TLogger; n: string);
    procedure DoWork;
  end;

constructor TLogger.Create(p: string);
begin
  prefix := p;
end;

procedure TLogger.Log(msg: string);
begin
  writeln('[', prefix, '] ', msg);
end;

constructor TService.Create(l: TLogger; n: string);
begin
  logger := l;   // inyección real del dependency vía el constructor
  name := n;
end;

procedure TService.DoWork;
begin
  logger.Log(name + ' arrancando...');
  logger.Log(name + ' listo');
end;

var
  lg: TLogger;
  svc1, svc2: TService;
begin
  lg := TLogger.Create('APP');

  svc1 := TService.Create(lg, 'ServicioA');
  svc2 := TService.Create(lg, 'ServicioB');

  svc1.DoWork();
  svc2.DoWork();
end.
