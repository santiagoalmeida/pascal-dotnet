using PascalCompiler;

if (args.Length < 1)
{
    Console.Error.WriteLine("Uso: pascalc <archivo.pas> [-o salida.dll]");
    return 1;
}

var inputPath = args[0];
if (!File.Exists(inputPath))
{
    Console.Error.WriteLine($"No se encontró el archivo '{inputPath}'.");
    return 1;
}

string outputPath = Path.ChangeExtension(Path.GetFileName(inputPath), ".dll");
for (int i = 1; i < args.Length; i++)
{
    if (args[i] == "-o" && i + 1 < args.Length)
    {
        outputPath = args[i + 1];
        i++;
    }
}

var source = File.ReadAllText(inputPath);

try
{
    var tokens = new Lexer(source).Tokenize();
    var program = new Parser(tokens).ParseProgram();
    new CodeGen().Compile(program, outputPath);

    // Programs using HttpXxx builtins call into PascalRuntime.dll at runtime; ship it
    // alongside the emitted assembly so `dotnet <output>.dll` can resolve the reference.
    var runtimeDllSource = Path.Combine(AppContext.BaseDirectory, "PascalRuntime.dll");
    var outputDir = Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? ".";
    var runtimeDllDest = Path.Combine(outputDir, "PascalRuntime.dll");
    if (File.Exists(runtimeDllSource) && runtimeDllSource != runtimeDllDest)
        File.Copy(runtimeDllSource, runtimeDllDest, overwrite: true);

    Console.WriteLine($"Compilado correctamente -> {Path.GetFullPath(outputPath)}");
    Console.WriteLine($"Ejecutar con: dotnet {outputPath}");
    return 0;
}
catch (LexError ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}
catch (ParseError ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}
catch (SemanticError ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}
