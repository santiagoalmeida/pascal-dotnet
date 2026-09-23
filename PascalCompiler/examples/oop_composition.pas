program OopComposition;

type
  TLogger = class
    prefix: string;
    procedure Log(msg: string);
  end;

  TService = class
    logger: TLogger;
    procedure DoWork;
  end;

procedure TLogger.Log(msg: string);
begin
  writeln('[', prefix, '] ', msg);
end;

procedure TService.DoWork;
begin
  // 'logger' es un campo implícito de Self: se lo llama como si fuera
  // una variable de objeto normal, sin calificar con 'Self.'.
  logger.Log('trabajando...');
  logger.Log('listo');
end;

var
  svc: TService;
  lg: TLogger;
begin
  lg := TLogger.Create();
  lg.prefix := 'SVC';

  svc := TService.Create();
  svc.logger := lg; // "inyectamos" la dependencia asignando el campo

  svc.DoWork();
end.
