# Pascal.NET

Un compilador de **Pascal a CIL** (el bytecode de .NET) escrito desde cero en C#. No es un intérprete: compila `.pas` a un ensamblado `.dll` real y ejecutable con `dotnet`, usando únicamente `System.Reflection.Emit` — sin `ilasm`, sin herramientas externas, sin dependencias de terceros.

Incluye, además del lenguaje, un pequeño **framework web incorporado** (`HttpXxx`) que permite escribir APIs HTTP reales en Pascal, con soporte de JSON, autenticación **JWT** y hashing seguro de contraseñas (**PBKDF2**).

```pascal
program MiApi;
begin
  HttpStart(8080);
  while HttpWait() do
  begin
    HttpSetHeader('Content-Type', 'application/json');
    if HttpPath() = '/hello' then
    begin
      HttpSetStatus(200);
      HttpWrite('{"mensaje": "Hola desde Pascal"}');
    end
    else
    begin
      HttpSetStatus(404);
      HttpWrite('{"error": "not found"}');
    end;
    HttpEnd();
  end;
end.
```

## ¿Por qué existe esto?

.NET solo mantiene C#, VB.NET y F# como lenguajes de primera clase. No hay una plantilla oficial de Pascal, y las alternativas de terceros (como Oxygene de RemObjects) son herramientas comerciales cerradas. Este proyecto nació como un experimento para responder: *¿qué tan lejos se puede llegar escribiendo un compilador de Pascal real, generando CIL directamente, sin depender de nada más que el propio SDK de .NET?*

## Arquitectura

```
código .pas
    │
    ▼
  Lexer          (Lexer.cs)     — tokeniza el código fuente
    │
    ▼
  Parser         (Parser.cs)    — descenso recursivo, construye el AST
    │
    ▼
  AST            (Ast.cs)       — árbol de sintaxis (records de C#)
    │
    ▼
  CodeGen        (CodeGen.cs)   — inferencia de tipos + emisión de IL
    │                             vía System.Reflection.Emit.PersistedAssemblyBuilder
    ▼
  .dll ejecutable + .runtimeconfig.json
```

`PersistedAssemblyBuilder` es una API nueva de .NET 9/10 que permite emitir un ensamblado real a disco (con punto de entrada, listo para `dotnet run`) sin pasar por texto IL intermedio ni `ilasm`.

### Estructura del repositorio

| Carpeta | Qué es |
|---|---|
| `PascalCompiler/` | El compilador (`pascalc`). Lexer, parser, AST y generador de código. |
| `PascalRuntime/` | Librería de soporte en C# que respalda las funciones incorporadas `HttpXxx`, `JsonXxx`, `JwtXxx` y de seguridad. El programa Pascal compilado la referencia en tiempo de ejecución. |
| `PascalCompiler/examples/` | Programas `.pas` de ejemplo, usados también como pruebas de humo del compilador. |

## Uso

```bash
cd PascalCompiler
dotnet run -- examples/hello.pas -o hello.dll
dotnet hello.dll
```

Si el programa usa alguna función `HttpXxx`, el compilador copia automáticamente `PascalRuntime.dll` junto al `.dll` generado, para que corra de forma standalone.

Requisitos: .NET SDK 10 o superior (por `PersistedAssemblyBuilder`).

## El lenguaje

Un subconjunto de Pascal clásico/Object Pascal, con las siguientes construcciones:

### Tipos

`integer` (Int32), `real` (Double), `boolean`, `string`. Promoción automática `integer` → `real` en asignaciones y expresiones mixtas.

### Variables y control de flujo

```pascal
var
  x, y: integer;
  nombre: string;
begin
  x := 5;
  if x > 3 then
    writeln('mayor')
  else
    writeln('menor o igual');

  while x > 0 do
  begin
    writeln(x);
    x := x - 1;
  end;

  for x := 1 to 10 do
    writeln(x);

  case x of
    0: writeln('cero');
    1, 2: writeln('uno o dos');
    else writeln('otro');
  end;
end.
```

### Funciones, procedimientos y recursión

```pascal
function Factorial(n: integer): integer;
begin
  if n <= 1 then
    Result := 1
  else
    Result := n * Factorial(n - 1);
end;
```

El valor de retorno se asigna a la variable implícita `Result`. Las funciones/procedimientos se compilan en dos pasadas (firmas primero, cuerpos después), lo que permite recursión directa, mutua y llamadas hacia adelante.

### Parámetros por referencia (`var`)

```pascal
procedure Swap(var a: integer; var b: integer);
var temp: integer;
begin
  temp := a; a := b; b := temp;
end;
```

También soportado con elementos de array como argumento: `Swap(arr[i], arr[j])`.

### Arrays (1D y 2D)

```pascal
var
  nums: array[1..10] of integer;
  matriz: array[1..3, 1..3] of integer;
begin
  nums[1] := 42;
  matriz[2, 2] := 5;
end.
```

Los límites (`low..high`) son arbitrarios, no solo base-0. Los arrays 2D se implementan como jagged arrays (`T[][]`) por debajo de un array rectangular real, para simplificar el codegen.

### Records

```pascal
type
  TPunto = record
    x: integer;
    y: integer;
  end;

function CrearPunto(px, py: integer): TPunto;
begin
  Result.x := px;
  Result.y := py;
end;
```

Los records pueden usarse como parámetros y como tipo de retorno de función.

### Clases (OOP)

```pascal
type
  TCounter = class
    value: integer;
    procedure Increment;
    function GetValue: integer;
  end;

procedure TCounter.Increment;
begin
  value := value + 1;   // campo implícito: equivale a Self.value
end;

function TCounter.GetValue: integer;
begin
  Result := value;
end;

var
  c: TCounter;
begin
  c := TCounter.Create();
  c.Increment();
  c.Increment();
  writeln(c.GetValue());  // 2
end.
```

Los métodos se compilan como métodos de instancia **reales** de CLR (no procedimientos estáticos con un parámetro extra) — `Self` es el `this` implícito, y las llamadas usan `callvirt`. `TClase.Create()` construye una instancia nueva; una variable de tipo clase empieza en `nil` hasta que se le asigna una.

#### Encapsulamiento (`private`/`public`)

```pascal
type
  TAccount = class
  private
    balance: integer;
  public
    procedure Deposit(amount: integer);
    function GetBalance: integer;
  end;
```

Un miembro `private` solo es accesible desde dentro de un método de la **misma clase que lo declaró** (ni siquiera una subclase puede tocarlo) — acceder desde afuera es un error de compilación, no una convención.

#### Composición de objetos e inyección de dependencias

Un campo de clase puede ser de tipo `record` o `class`, no solo escalar:

```pascal
type
  TLogger = class
    procedure Log(msg: string);
  end;

  TService = class
    logger: TLogger;
    constructor Create(l: TLogger);
    procedure DoWork;
  end;

constructor TService.Create(l: TLogger);
begin
  logger := l;              // inyección real por constructor
end;

procedure TService.DoWork;
begin
  logger.Log('trabajando'); // campo-objeto implícito, usado como si fuera una variable
end;
```

`constructor Create(...)` admite parámetros (incluidos objetos), con implementación separada igual que los métodos — es lo que hace viable la inyección de dependencias real, no solo asignar el campo a mano después de `Create()`.

**Límite:** un campo-objeto se puede asignar (desde otra variable o `Create()`), pero no encadenar otro nivel (`obj.campo.campo2` no está soportado).

#### Herencia, `virtual`/`override`, `inherited`

```pascal
type
  TAnimal = class
    name: string;
    procedure Speak; virtual;
  end;

  TDog = class(TAnimal)
    procedure Speak; override;
  end;

procedure TDog.Speak;
begin
  inherited Speak;              // comportamiento base primero...
  writeln(name, ' dice: Guau!'); // ...despues el propio
end;

var
  a: TAnimal;
  d: TDog;
begin
  d := TDog.Create();
  a := d;      // una variable TAnimal puede apuntar a un TDog
  a.Speak();   // despacha a TDog.Speak en tiempo de ejecución — polimorfismo real
end.
```

El despacho es polimorfismo real de CLR (`MethodAttributes.Virtual` + `DefineMethodOverride`), no resolución de nombres en tiempo de compilación — una variable declarada como la clase base puede contener una instancia de cualquier subclase, y el método que se ejecuta es el de la clase concreta del objeto. `inherited Metodo(args)` llama a la versión base directamente (sin volver a pasar por el despacho virtual).

### Funciones incorporadas

| Función | Firma | Descripción |
|---|---|---|
| `Length` | `(s: string): integer` | Largo de un string |
| `Copy` | `(s: string; inicio, cant: integer): string` | Subcadena (índice base-1) |
| `UpperCase` / `LowerCase` | `(s: string): string` | Cambio de mayúsculas/minúsculas |
| `Trim` | `(s: string): string` | Recorta espacios |
| `IntToStr` / `FloatToStr` | `(n): string` | Conversión numérica → string |
| `StrToInt` / `StrToFloat` | `(s: string): integer/real` | Conversión string → numérico |

### Módulo HTTP (`HttpXxx`)

Un servidor HTTP real (`System.Net.HttpListener`, portable a Linux/macOS/Windows), expuesto como funciones/procedimientos imperativos — sin objetos ni lambdas:

| Nombre | Firma | Descripción |
|---|---|---|
| `HttpStart` | `procedure(port: integer)` | Arranca el listener |
| `HttpWait` | `function: boolean` | Bloquea hasta la próxima request |
| `HttpMethod` | `function: string` | Método HTTP de la request actual |
| `HttpPath` | `function: string` | Path de la URL |
| `HttpQuery` | `function(clave: string): string` | Parámetro de query string |
| `HttpMatch` | `function(patron: string): boolean` | Matchea rutas tipo `/users/:id` |
| `HttpParam` | `function(nombre: string): string` | Segmento capturado por `HttpMatch` |
| `HttpHeader` | `function(nombre: string): string` | Header arbitrario de la request |
| `HttpBearerToken` | `function: string` | Extrae el token de `Authorization: Bearer ...` |
| `HttpBody` | `function: string` | Body crudo de la request (cacheado por request) |
| `HttpSetStatus` | `procedure(codigo: integer)` | Código de estado de la respuesta |
| `HttpSetHeader` | `procedure(nombre, valor: string)` | Header de la respuesta |
| `HttpWrite` | `procedure(texto: string)` | Escribe al body de la respuesta |
| `HttpEnd` | `procedure` | Cierra la respuesta |

### JSON (`JsonXxx`)

| Nombre | Firma | Descripción |
|---|---|---|
| `JsonGetString` | `function(json, clave: string): string` | Lee un campo string de un JSON plano |
| `JsonGetInt` | `function(json, clave: string): integer` | Lee un campo entero |
| `JsonEscape` | `function(s: string): string` | Devuelve `s` como literal JSON válido y escapado (con comillas) |

### JWT y seguridad (`JwtXxx`, `HashPassword`, `VerifyPassword`)

JWT **HS256** real (header.payload.firma, Base64Url, HMAC-SHA256) y hashing de contraseñas con **PBKDF2-HMACSHA256** (100,000 iteraciones + salt aleatorio de 16 bytes) — ambos usando solo `System.Security.Cryptography` de la BCL, sin dependencias externas.

| Nombre | Firma | Descripción |
|---|---|---|
| `JwtSign` | `function(payloadJson, secreto: string): string` | Firma un JWT HS256 |
| `JwtVerify` | `function(token, secreto: string): boolean` | Verifica la firma (comparación en tiempo constante) |
| `JwtPayload` | `function(token: string): string` | Decodifica el payload (JSON) |
| `JwtNow` | `function: integer` | Timestamp Unix actual, para claims `exp` |
| `HashPassword` | `function(password: string): string` | Hash PBKDF2 con salt embebido |
| `VerifyPassword` | `function(password, hash: string): boolean` | Verifica una contraseña contra su hash |

Ver `PascalCompiler/examples/api_auth.pas` para un flujo completo de login + JWT protegiendo una ruta.

## Ejemplos incluidos

| Archivo | Qué demuestra |
|---|---|
| `hello.pas` | Lo básico: variables, `if`, `writeln` |
| `loop.pas` | `while` anidado — criba de primos |
| `procedimientos.pas` | `procedure`/`function` con parámetros, `for`, recursión (factorial) |
| `fibonacci_naive.pas` | Un programa **real de GitHub** ([modern-pascal-course](https://github.com/modern-pascal/modern-pascal-course)) — Fibonacci recursivo con `case`, `for` y `Readln` |
| `arrays_var.pas` | Parámetros `var`, arrays, bubble sort con `Swap(arr[i], arr[j])` |
| `records_strings.pas` | `record`, funciones incorporadas de string/conversión |
| `matrix_records.pas` | Arrays 2D (matrices) + records como parámetro/retorno de función |
| `oop_counter.pas` | `class` con campos y métodos, `Self` implícito, instancias independientes |
| `oop_rectangle.pas` | Métodos llamándose entre sí vía `Self.Metodo()`, método booleano usado en `if` |
| `oop_bank_account.pas` | Objetos pasados como parámetro a una función libre (semántica de referencia) |
| `oop_task.pas` | Reasignar `TClase.Create()` a la misma variable, campos `string`/`boolean` |
| `oop_encapsulation.pas` | `private`/`public` — acceso externo a un campo privado rechazado en compilación |
| `oop_composition.pas` | Campo de tipo objeto (`logger: TLogger`), usado como dependencia inyectada a mano |
| `oop_ctor_injection.pas` | `constructor Create(...)` con parámetros — inyección de dependencias real |
| `oop_inheritance.pas` | `class(TPadre)` — campos/métodos heredados, usados desde afuera y desde `Self` |
| `oop_polymorphism.pas` | `virtual`/`override`/`inherited` — despacho polimórfico real (no solo estático) |
| `api.pas` | API HTTP con rutas parametrizadas y JSON |
| `api_auth.pas` | Login, hashing de contraseñas y rutas protegidas con JWT |

## Limitaciones conocidas / decisiones de diseño

Este es un proyecto experimental, no un compilador de producción. Simplificaciones deliberadas, documentadas también en el código:

- **Arrays y records se comparten por referencia**, incluso sin `var` — se aparta de la semántica de valor estricta de Pascal, pero simplifica enormemente el codegen (son tipos `class`/array de .NET, no `struct`).
- **Arrays 2D** son jagged arrays (`T[][]`), no arrays rectangulares nativos (`T[,]`).
- **No hay arrays de N dimensiones genéricas** (solo 1D y 2D), ni records anidados, ni records con campos array.
- **Un campo-objeto de clase no se puede encadenar** (`obj.campo.campo2`) — solo asignar o usar como receptor de un método/campo de un solo nivel.
- **Un método de instancia solo declara parámetros escalares** en su firma de clase — pasar objetos como argumento sí funciona (se valida en la llamada), pero no está chequeado a nivel de la declaración del método en sí.
- **Un constructor con parámetros no se encadena automáticamente** cuando la clase base también tiene uno — hace falta declarar el propio `constructor Create` (no hay `inherited Create(args)` para constructores, solo para métodos normales).
- **Sin genéricos** (`TFoo<T>`) — ni en clases ni en funciones.
- **`HttpListener` es de un solo hilo/secuencial** (`HttpWait()` bloquea) — no maneja requests concurrentes. Suficiente para aprender/prototipar, no para producción.
- **Sin manejo de excepciones** (`try/except`).
- **No hay interoperabilidad genérica con .NET** (no se puede llamar a cualquier clase de la BCL o un paquete NuGet) — solo el set fijo de funciones incorporadas documentado arriba. Esto descarta, por ejemplo, un ORM como Entity Framework: necesitaría genéricos, LINQ/expression trees y `async`/`await`, que son features de compilador en sí mismas.
- El compilador y sus mensajes de error están en **español**.

## Contribuciones

Este proyecto está abierto a contribuciones — PRs, issues, ideas de features o reportes de bugs son bienvenidos. Si vas a agregar una feature grande, abrí un issue primero para discutir el enfoque (el código sigue un estilo bastante directo: sin abstracciones prematuras, con comentarios solo donde el *por qué* no es obvio).

Ideas abiertas para quien quiera meter mano: genéricos, `try/except`, arrays de N dimensiones, records anidados/con campos array, encadenar campos-objeto (`obj.campo.campo2`), `inherited Create(args)` para constructores, un mini módulo `DbXxx` sobre ADO.NET (SQL crudo) para persistencia, un modo `--optimize`, tests automatizados.

## Licencia

MIT — ver [`LICENSE`](LICENSE). En resumen: podés usar, modificar, distribuir y hacer lo que quieras con este código, comercialmente o no, siempre que mantengas el aviso de copyright. Se provee "tal cual", sin garantía de ningún tipo.
