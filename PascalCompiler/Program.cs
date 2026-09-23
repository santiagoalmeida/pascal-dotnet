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

    // Programs using builtins (HttpXxx, DbXxx, ...) call into PascalRuntime.dll and its
    // own dependencies (e.g. Microsoft.Data.Sqlite + its native libraries) at runtime;
    // ship everything the compiler itself was built with alongside the emitted assembly
    // so `dotnet <output>.dll` can resolve all of it standalone.
    var outputDir = Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? ".";
    CopyRuntimeDependencies(AppContext.BaseDirectory, outputDir);

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

// Copies every file the compiler itself ships with (except its own dll/pdb/deps/
// runtimeconfig) into the emitted program's output directory. PascalCompiler.csproj
// builds against $(NETCoreSdkRuntimeIdentifier) (see the RuntimeIdentifier property),
// so MSBuild already places the correct platform-specific implementation of every
// dependency (e.g. libe_sqlite3.so, the real Microsoft.Data.SqlClient.dll instead of
// its "unsupported platform" stub) flat in AppContext.BaseDirectory — no separate
// "runtimes/<rid>/..." walk needed. The runtimes/ fallback below only matters if the
// compiler is ever built without a resolved RID (plain portable/framework-dependent).
static void CopyRuntimeDependencies(string sourceDir, string outputDir)
{
    foreach (var file in Directory.GetFiles(sourceDir))
    {
        var name = Path.GetFileName(file);
        if (name.StartsWith("PascalCompiler", StringComparison.OrdinalIgnoreCase)) continue;
        var dest = Path.Combine(outputDir, name);
        if (!string.Equals(Path.GetFullPath(file), Path.GetFullPath(dest), StringComparison.Ordinal))
            File.Copy(file, dest, overwrite: true);
    }

    var runtimesDir = Path.Combine(sourceDir, "runtimes");
    if (!Directory.Exists(runtimesDir)) return;

    var rid = System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier;
    var osFallback = rid.Split('-')[0];
    foreach (var ridCandidate in new[] { rid, osFallback })
        foreach (var kind in new[] { "native", "lib" })
        {
            var dir = Path.Combine(runtimesDir, ridCandidate, kind);
            if (!Directory.Exists(dir)) continue;
            foreach (var f in Directory.GetFiles(dir))
                File.Copy(f, Path.Combine(outputDir, Path.GetFileName(f)), overwrite: true);
        }
}
