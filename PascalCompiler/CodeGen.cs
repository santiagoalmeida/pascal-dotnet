using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace PascalCompiler;

public sealed class SemanticError(string message, int line, int col) : Exception($"Error semántico ({line}:{col}): {message}");

public sealed class CodeGen
{
    private static readonly MethodInfo StringConcat =
        typeof(string).GetMethod("Concat", new[] { typeof(string), typeof(string) })!;
    private static readonly MethodInfo StringEquals =
        typeof(string).GetMethod("Equals", new[] { typeof(string), typeof(string) })!;
    private static readonly MethodInfo ConsoleWriteLineVoid =
        typeof(Console).GetMethod("WriteLine", Type.EmptyTypes)!;
    private static readonly MethodInfo ConsoleReadLine =
        typeof(Console).GetMethod("ReadLine", Type.EmptyTypes)!;

    private static readonly MethodInfo StringLengthGet = typeof(string).GetProperty("Length")!.GetGetMethod()!;
    private static readonly MethodInfo StringSubstring = typeof(string).GetMethod("Substring", new[] { typeof(int), typeof(int) })!;
    private static readonly MethodInfo StringToUpper = typeof(string).GetMethod("ToUpperInvariant", Type.EmptyTypes)!;
    private static readonly MethodInfo StringToLower = typeof(string).GetMethod("ToLowerInvariant", Type.EmptyTypes)!;
    private static readonly MethodInfo StringTrim = typeof(string).GetMethod("Trim", Type.EmptyTypes)!;
    private static readonly MethodInfo ConvertIntToStr = typeof(Convert).GetMethod("ToString", new[] { typeof(int) })!;
    private static readonly MethodInfo ConvertDoubleToStr = typeof(Convert).GetMethod("ToString", new[] { typeof(double) })!;
    private static readonly MethodInfo ConvertStrToInt = typeof(Convert).GetMethod("ToInt32", new[] { typeof(string) })!;
    private static readonly MethodInfo ConvertStrToDouble = typeof(Convert).GetMethod("ToDouble", new[] { typeof(string) })!;

    private static readonly MethodInfo HttpStartMethod = typeof(PascalRuntime.Http).GetMethod("Start", new[] { typeof(int) })!;
    private static readonly MethodInfo HttpWaitMethod = typeof(PascalRuntime.Http).GetMethod("Wait", Type.EmptyTypes)!;
    private static readonly MethodInfo HttpMethodMethod = typeof(PascalRuntime.Http).GetMethod("Method", Type.EmptyTypes)!;
    private static readonly MethodInfo HttpPathMethod = typeof(PascalRuntime.Http).GetMethod("Path", Type.EmptyTypes)!;
    private static readonly MethodInfo HttpQueryMethod = typeof(PascalRuntime.Http).GetMethod("Query", new[] { typeof(string) })!;
    private static readonly MethodInfo HttpBodyMethod = typeof(PascalRuntime.Http).GetMethod("Body", Type.EmptyTypes)!;
    private static readonly MethodInfo HttpSetStatusMethod = typeof(PascalRuntime.Http).GetMethod("SetStatus", new[] { typeof(int) })!;
    private static readonly MethodInfo HttpSetHeaderMethod = typeof(PascalRuntime.Http).GetMethod("SetHeader", new[] { typeof(string), typeof(string) })!;
    private static readonly MethodInfo HttpWriteMethod = typeof(PascalRuntime.Http).GetMethod("Write", new[] { typeof(string) })!;
    private static readonly MethodInfo HttpEndMethod = typeof(PascalRuntime.Http).GetMethod("End", Type.EmptyTypes)!;
    private static readonly MethodInfo HttpMatchMethod = typeof(PascalRuntime.Http).GetMethod("Match", new[] { typeof(string) })!;
    private static readonly MethodInfo HttpParamMethod = typeof(PascalRuntime.Http).GetMethod("Param", new[] { typeof(string) })!;

    private static readonly MethodInfo JsonGetStringMethod = typeof(PascalRuntime.Json).GetMethod("GetString", new[] { typeof(string), typeof(string) })!;
    private static readonly MethodInfo JsonGetIntMethod = typeof(PascalRuntime.Json).GetMethod("GetInt", new[] { typeof(string), typeof(string) })!;
    private static readonly MethodInfo JsonEscapeMethod = typeof(PascalRuntime.Json).GetMethod("Escape", new[] { typeof(string) })!;

    private static readonly MethodInfo HttpHeaderMethod = typeof(PascalRuntime.Http).GetMethod("Header", new[] { typeof(string) })!;
    private static readonly MethodInfo HttpBearerTokenMethod = typeof(PascalRuntime.Http).GetMethod("BearerToken", Type.EmptyTypes)!;

    private static readonly MethodInfo JwtSignMethod = typeof(PascalRuntime.Jwt).GetMethod("Sign", new[] { typeof(string), typeof(string) })!;
    private static readonly MethodInfo JwtVerifyMethod = typeof(PascalRuntime.Jwt).GetMethod("Verify", new[] { typeof(string), typeof(string) })!;
    private static readonly MethodInfo JwtPayloadMethod = typeof(PascalRuntime.Jwt).GetMethod("Payload", new[] { typeof(string) })!;
    private static readonly MethodInfo JwtNowMethod = typeof(PascalRuntime.Jwt).GetMethod("Now", Type.EmptyTypes)!;

    private static readonly MethodInfo HashPasswordMethod = typeof(PascalRuntime.Security).GetMethod("HashPassword", new[] { typeof(string) })!;
    private static readonly MethodInfo VerifyPasswordMethod = typeof(PascalRuntime.Security).GetMethod("VerifyPassword", new[] { typeof(string), typeof(string) })!;

    private static readonly HashSet<string> BuiltinNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Length", "Copy", "UpperCase", "LowerCase", "Trim", "IntToStr", "FloatToStr", "StrToInt", "StrToFloat",
        "HttpWait", "HttpMethod", "HttpPath", "HttpQuery", "HttpBody", "HttpMatch", "HttpParam",
        "HttpHeader", "HttpBearerToken",
        "JsonGetString", "JsonGetInt", "JsonEscape",
        "JwtSign", "JwtVerify", "JwtPayload", "JwtNow",
        "HashPassword", "VerifyPassword",
    };

    private static readonly HashSet<string> BuiltinProcNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "HttpStart", "HttpSetStatus", "HttpSetHeader", "HttpWrite", "HttpEnd",
    };

    private enum SlotKind { Local, Arg }

    private readonly record struct VarSlot(
        PascalType Type,
        SlotKind Kind,
        LocalBuilder? Local,
        int ArgIndex,
        bool IsByRef = false,
        ArrayInfo? Array = null,
        string? RecordType = null,
        string? ClassType = null);

    private sealed record FuncInfo(MethodBuilder Method, PascalType? ReturnType, string? ReturnRecordType, List<ParamDecl> Params)
    {
        public bool IsFunction => ReturnType.HasValue || ReturnRecordType is not null;
    }

    private sealed record RecordTypeInfo(
        TypeBuilder Type,
        ConstructorBuilder Ctor,
        Dictionary<string, (FieldBuilder Field, PascalType Type)> Fields);

    private sealed record ClassMethodInfo(MethodBuilder Method, PascalType? ReturnType, List<ParamDecl> Params, bool IsPrivate);

    private sealed record ClassTypeInfo(
        TypeBuilder Type,
        ConstructorBuilder Ctor,
        Dictionary<string, (FieldBuilder Field, PascalType Type, bool IsPrivate)> Fields,
        Dictionary<string, ClassMethodInfo> Methods);

    private ILGenerator _il = null!;
    private Dictionary<string, VarSlot> _symbols = null!;
    private ClassTypeInfo? _currentClass; // set while compiling a class method body, for implicit Self.field access
    private readonly Dictionary<string, FuncInfo> _functions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RecordTypeInfo> _recordTypes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ClassTypeInfo> _classTypes = new(StringComparer.OrdinalIgnoreCase);

    public void Compile(PascalProgram program, string outputPath)
    {
        var asmName = new AssemblyName(SanitizeIdentifier(program.Name));
        var asmBuilder = new PersistedAssemblyBuilder(asmName, typeof(object).Assembly);
        var moduleBuilder = asmBuilder.DefineDynamicModule(asmName.Name!);
        var typeBuilder = moduleBuilder.DefineType("Program", TypeAttributes.Public | TypeAttributes.Class);

        // Pass 0: declare record types (as reference-type classes; see summary caveat on value semantics).
        var seenRecordType = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rt in program.RecordTypes)
        {
            if (!seenRecordType.Add(rt.Name))
                throw new SemanticError($"el tipo '{rt.Name}' ya está declarado", rt.Line, rt.Col);

            var recTypeBuilder = moduleBuilder.DefineType(rt.Name, TypeAttributes.Public | TypeAttributes.Class);
            var ctor = recTypeBuilder.DefineDefaultConstructor(MethodAttributes.Public);
            var fields = new Dictionary<string, (FieldBuilder, PascalType)>(StringComparer.OrdinalIgnoreCase);

            var seenField = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in rt.Fields)
            {
                if (!seenField.Add(f.Name))
                    throw new SemanticError($"el campo '{f.Name}' ya está declarado en '{rt.Name}'", f.Line, f.Col);
                var fb = recTypeBuilder.DefineField(f.Name, ClrType(f.Type), FieldAttributes.Public);
                fields[f.Name] = (fb, f.Type);
            }

            _recordTypes[rt.Name] = new RecordTypeInfo(recTypeBuilder, ctor, fields);
        }

        // Pass 0.5: declare class types — fields and method *signatures* (as real, non-static
        // CLR instance methods; Self is the implicit CLR "this"). Bodies come in Pass 2.5,
        // once every class and free-function signature exists (recursion, forward refs).
        var seenClassType = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var ct in program.ClassTypes)
        {
            if (!seenClassType.Add(ct.Name) || _recordTypes.ContainsKey(ct.Name))
                throw new SemanticError($"el tipo '{ct.Name}' ya está declarado", ct.Line, ct.Col);

            var classTypeBuilder = moduleBuilder.DefineType(ct.Name, TypeAttributes.Public | TypeAttributes.Class);
            var ctor = classTypeBuilder.DefineDefaultConstructor(MethodAttributes.Public);

            var fields = new Dictionary<string, (FieldBuilder, PascalType, bool)>(StringComparer.OrdinalIgnoreCase);
            var seenField = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in ct.Fields)
            {
                if (!seenField.Add(f.Name))
                    throw new SemanticError($"el campo '{f.Name}' ya está declarado en '{ct.Name}'", f.Line, f.Col);
                var fb = classTypeBuilder.DefineField(f.Name, ClrType(f.Type), FieldAttributes.Public);
                fields[f.Name] = (fb, f.Type, f.IsPrivate);
            }

            var methods = new Dictionary<string, ClassMethodInfo>(StringComparer.OrdinalIgnoreCase);
            var seenMethod = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in ct.Methods)
            {
                if (!seenMethod.Add(m.Name))
                    throw new SemanticError($"el método '{m.Name}' ya está declarado en '{ct.Name}'", m.Line, m.Col);
                if (fields.ContainsKey(m.Name))
                    throw new SemanticError($"'{m.Name}' ya está declarado como campo en '{ct.Name}'", m.Line, m.Col);

                var mParamTypes = m.Params.Select(ParamClrType).ToArray();
                var mReturnClr = m.ReturnType.HasValue ? ClrType(m.ReturnType.Value) : typeof(void);
                var methodBuilder = classTypeBuilder.DefineMethod(
                    m.Name,
                    MethodAttributes.Public | MethodAttributes.HideBySig, // instance method: Self is the implicit CLR "this"
                    mReturnClr,
                    mParamTypes);

                for (int i = 0; i < m.Params.Count; i++)
                    methodBuilder.DefineParameter(i + 1, ParameterAttributes.None, m.Params[i].Name);

                methods[m.Name] = new ClassMethodInfo(methodBuilder, m.ReturnType, m.Params, m.IsPrivate);
            }

            _classTypes[ct.Name] = new ClassTypeInfo(classTypeBuilder, ctor, fields, methods);
        }

        // Pass 1: declare all function/procedure signatures so calls (incl. recursive and forward) resolve.
        var seenSub = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sub in program.Subs)
        {
            if (!seenSub.Add(sub.Name))
                throw new SemanticError($"'{sub.Name}' ya está declarado", sub.Line, sub.Col);
            if (BuiltinNames.Contains(sub.Name) || BuiltinProcNames.Contains(sub.Name))
                throw new SemanticError($"'{sub.Name}' es una función/procedimiento incorporado del compilador; no se puede redeclarar", sub.Line, sub.Col);

            var paramTypes = sub.Params.Select(ParamClrType).ToArray();

            Type returnClrType;
            if (sub.ReturnRecordType is not null)
            {
                if (!_recordTypes.TryGetValue(sub.ReturnRecordType, out var recRt))
                    throw new SemanticError($"tipo '{sub.ReturnRecordType}' no declarado", sub.Line, sub.Col);
                returnClrType = recRt.Type;
            }
            else
            {
                returnClrType = sub.ReturnType.HasValue ? ClrType(sub.ReturnType.Value) : typeof(void);
            }

            var method = typeBuilder.DefineMethod(
                sub.Name,
                MethodAttributes.Public | MethodAttributes.Static,
                returnClrType,
                paramTypes);

            for (int i = 0; i < sub.Params.Count; i++)
                method.DefineParameter(i + 1, ParameterAttributes.None, sub.Params[i].Name);

            _functions[sub.Name] = new FuncInfo(method, sub.ReturnType, sub.ReturnRecordType, sub.Params);
        }

        // Pass 2: emit bodies for each function/procedure.
        foreach (var sub in program.Subs)
            CompileSub(sub);

        // Pass 2.5: emit bodies for class method implementations declared elsewhere in the file.
        var seenImpl = new HashSet<(string ClassName, string MethodName)>();
        foreach (var impl in program.MethodImpls)
        {
            if (!_classTypes.TryGetValue(impl.ClassName, out var classInfo))
                throw new SemanticError($"la clase '{impl.ClassName}' no está declarada", impl.Line, impl.Col);
            if (!classInfo.Methods.TryGetValue(impl.MethodName, out var methodInfo))
                throw new SemanticError($"'{impl.ClassName}' no declara un método '{impl.MethodName}'", impl.Line, impl.Col);
            if (!seenImpl.Add((impl.ClassName, impl.MethodName)))
                throw new SemanticError($"'{impl.ClassName}.{impl.MethodName}' ya tiene una implementación", impl.Line, impl.Col);

            bool paramsMatch = impl.Params.Count == methodInfo.Params.Count &&
                impl.Params.Zip(methodInfo.Params).All(p => p.First.Type == p.Second.Type && p.First.ByRef == p.Second.ByRef);
            if (!paramsMatch || impl.ReturnType != methodInfo.ReturnType)
                throw new SemanticError($"la implementación de '{impl.ClassName}.{impl.MethodName}' no coincide con su firma declarada en la clase", impl.Line, impl.Col);

            CompileMethodImpl(classInfo, methodInfo, impl);
        }
        foreach (var ct in program.ClassTypes)
            foreach (var m in ct.Methods)
                if (!seenImpl.Contains((ct.Name, m.Name)))
                    throw new SemanticError($"falta la implementación de '{ct.Name}.{m.Name}'", m.Line, m.Col);

        // Pass 3: emit Main, which hosts the program's global vars and top-level statements.
        var mainMethod = typeBuilder.DefineMethod(
            "Main",
            MethodAttributes.Public | MethodAttributes.Static,
            typeof(void),
            new[] { typeof(string[]) });

        _il = mainMethod.GetILGenerator();
        _symbols = new Dictionary<string, VarSlot>(StringComparer.OrdinalIgnoreCase);

        foreach (var v in program.Vars)
        {
            if (_symbols.ContainsKey(v.Name))
                throw new SemanticError($"variable '{v.Name}' ya declarada", v.Line, v.Col);
            DeclareLocalVar(v);
        }

        EmitStmt(program.Body);
        _il.Emit(OpCodes.Ret);

        foreach (var rt in _recordTypes.Values)
            rt.Type.CreateType();
        foreach (var ct in _classTypes.Values)
            ct.Type.CreateType();
        typeBuilder.CreateType();

        var metadataBuilder = asmBuilder.GenerateMetadata(out var ilStream, out var fieldData);
        var peHeaderBuilder = new PEHeaderBuilder(imageCharacteristics: Characteristics.ExecutableImage);
        var peBuilder = new ManagedPEBuilder(
            header: peHeaderBuilder,
            metadataRootBuilder: new MetadataRootBuilder(metadataBuilder),
            ilStream: ilStream,
            mappedFieldData: fieldData,
            entryPoint: MetadataTokens.MethodDefinitionHandle(mainMethod.MetadataToken));

        var peBlob = new BlobBuilder();
        peBuilder.Serialize(peBlob);

        using (var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
        {
            peBlob.WriteContentTo(fs);
        }

        var dir = Path.GetDirectoryName(outputPath);
        var baseName = Path.GetFileNameWithoutExtension(outputPath);
        var rtConfigPath = Path.Combine(string.IsNullOrEmpty(dir) ? "." : dir, baseName + ".runtimeconfig.json");
        File.WriteAllText(rtConfigPath, BuildRuntimeConfig());
    }

    private void CompileSub(SubDecl sub)
    {
        var info = _functions[sub.Name];
        _il = info.Method.GetILGenerator();
        _symbols = new Dictionary<string, VarSlot>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < sub.Params.Count; i++)
        {
            var p = sub.Params[i];
            if (p.RecordType is not null)
                _symbols[p.Name] = MakeNamedTypeSlot(p.RecordType, SlotKind.Arg, null, i, p.Line, p.Col);
            else if (p.Array is not null)
                _symbols[p.Name] = new VarSlot(p.Array.ElementType, SlotKind.Arg, null, i, false, p.Array);
            else
                _symbols[p.Name] = new VarSlot(p.Type, SlotKind.Arg, null, i, p.ByRef);
        }

        LocalBuilder? resultLocal = null;
        if (sub.ReturnRecordType is not null)
        {
            var rec = _recordTypes[sub.ReturnRecordType]; // existence already validated in Pass 1
            resultLocal = _il.DeclareLocal(rec.Type);
            _il.Emit(OpCodes.Newobj, rec.Ctor);
            _il.Emit(OpCodes.Stloc, resultLocal);
            _symbols["Result"] = new VarSlot(PascalType.Void, SlotKind.Local, resultLocal, -1, false, null, sub.ReturnRecordType);
        }
        else if (sub.ReturnType.HasValue)
        {
            resultLocal = _il.DeclareLocal(ClrType(sub.ReturnType.Value));
            _symbols["Result"] = new VarSlot(sub.ReturnType.Value, SlotKind.Local, resultLocal, -1);
        }

        foreach (var v in sub.Locals)
        {
            if (_symbols.ContainsKey(v.Name))
                throw new SemanticError($"'{v.Name}' ya está declarado en '{sub.Name}'", v.Line, v.Col);
            DeclareLocalVar(v);
        }

        EmitStmt(sub.Body);

        if (resultLocal is not null)
            _il.Emit(OpCodes.Ldloc, resultLocal);
        _il.Emit(OpCodes.Ret);
    }

    // Builds a VarSlot for a variable/param/Result declared with a named type (record or
    // class) — the AST only carries the type name; here we know which kind it resolves to.
    private VarSlot MakeNamedTypeSlot(string typeName, SlotKind kind, LocalBuilder? local, int argIndex, int line, int col)
    {
        if (_recordTypes.ContainsKey(typeName))
            return new VarSlot(PascalType.Void, kind, local, argIndex, false, null, typeName, null);
        if (_classTypes.ContainsKey(typeName))
            return new VarSlot(PascalType.Void, kind, local, argIndex, false, null, null, typeName);
        throw new SemanticError($"tipo '{typeName}' no declarado", line, col);
    }

    private void CompileMethodImpl(ClassTypeInfo classInfo, ClassMethodInfo methodInfo, MethodImplDecl impl)
    {
        _il = methodInfo.Method.GetILGenerator();
        _symbols = new Dictionary<string, VarSlot>(StringComparer.OrdinalIgnoreCase);
        _currentClass = classInfo;

        // Self is CLR arg 0 on a non-static instance method; declared params start at arg 1.
        _symbols["Self"] = new VarSlot(PascalType.Void, SlotKind.Arg, null, 0, false, null, null, impl.ClassName);
        for (int i = 0; i < impl.Params.Count; i++)
        {
            var p = impl.Params[i];
            if (p.RecordType is not null)
                _symbols[p.Name] = MakeNamedTypeSlot(p.RecordType, SlotKind.Arg, null, i + 1, p.Line, p.Col);
            else if (p.Array is not null)
                _symbols[p.Name] = new VarSlot(p.Array.ElementType, SlotKind.Arg, null, i + 1, false, p.Array);
            else
                _symbols[p.Name] = new VarSlot(p.Type, SlotKind.Arg, null, i + 1, p.ByRef);
        }

        LocalBuilder? resultLocal = null;
        if (impl.ReturnType.HasValue)
        {
            resultLocal = _il.DeclareLocal(ClrType(impl.ReturnType.Value));
            _symbols["Result"] = new VarSlot(impl.ReturnType.Value, SlotKind.Local, resultLocal, -1);
        }

        foreach (var v in impl.Locals)
        {
            if (_symbols.ContainsKey(v.Name))
                throw new SemanticError($"'{v.Name}' ya está declarado en '{impl.ClassName}.{impl.MethodName}'", v.Line, v.Col);
            DeclareLocalVar(v);
        }

        EmitStmt(impl.Body);

        if (resultLocal is not null)
            _il.Emit(OpCodes.Ldloc, resultLocal);
        _il.Emit(OpCodes.Ret);

        _currentClass = null;
    }

    private static string BuildRuntimeConfig()
    {
        var version = Environment.Version.ToString();
        return $$"""
        {
          "runtimeOptions": {
            "tfm": "net10.0",
            "framework": {
              "name": "Microsoft.NETCore.App",
              "version": "{{version}}"
            },
            "rollForward": "Minor"
          }
        }
        """;
    }

    private static string SanitizeIdentifier(string name)
    {
        var chars = name.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray();
        var result = new string(chars);
        return result.Length == 0 ? "PascalProgram" : result;
    }

    private static Type ClrType(PascalType t) => t switch
    {
        PascalType.Integer => typeof(int),
        PascalType.Real => typeof(double),
        PascalType.Boolean => typeof(bool),
        PascalType.StringT => typeof(string),
        _ => throw new InvalidOperationException($"tipo no soportado: {t}"),
    };

    private Type ParamClrType(ParamDecl p)
    {
        if (p.RecordType is not null)
        {
            // reference type either way; 'var' is a no-op on it, same as arrays
            if (_recordTypes.TryGetValue(p.RecordType, out var rec)) return rec.Type;
            if (_classTypes.TryGetValue(p.RecordType, out var cls)) return cls.Type;
            throw new SemanticError($"tipo '{p.RecordType}' no declarado", p.Line, p.Col);
        }
        if (p.Array is not null)
        {
            var elemClr = ClrType(p.Array.ElementType);
            // arrays are already reference types; 'var' is a no-op on them
            return p.Array.Is2D ? elemClr.MakeArrayType().MakeArrayType() : elemClr.MakeArrayType();
        }
        var clr = ClrType(p.Type);
        return p.ByRef ? clr.MakeByRefType() : clr;
    }

    private void DeclareLocalVar(VarDecl v)
    {
        if (v.RecordType is not null)
        {
            if (_recordTypes.TryGetValue(v.RecordType, out var rec))
            {
                var local = _il.DeclareLocal(rec.Type);
                _il.Emit(OpCodes.Newobj, rec.Ctor);
                _il.Emit(OpCodes.Stloc, local);
                _symbols[v.Name] = new VarSlot(PascalType.Void, SlotKind.Local, local, -1, false, null, v.RecordType, null);
                return;
            }
            if (_classTypes.TryGetValue(v.RecordType, out var cls))
            {
                // Real object semantics: starts as null (CLR zero-inits locals); the program
                // must call TClase.Create() explicitly before using it.
                var local = _il.DeclareLocal(cls.Type);
                _symbols[v.Name] = new VarSlot(PascalType.Void, SlotKind.Local, local, -1, false, null, null, v.RecordType);
                return;
            }
            throw new SemanticError($"tipo '{v.RecordType}' no declarado", v.Line, v.Col);
        }
        if (v.Array is not null && v.Array.Is2D)
        {
            var elemClr = ClrType(v.Array.ElementType);
            var innerClr = elemClr.MakeArrayType();
            var local = _il.DeclareLocal(innerClr.MakeArrayType());
            int outerLen = v.Array.High - v.Array.Low + 1;
            int innerLen = v.Array.High2!.Value - v.Array.Low2!.Value + 1;

            _il.Emit(OpCodes.Ldc_I4, outerLen);
            _il.Emit(OpCodes.Newarr, innerClr);
            _il.Emit(OpCodes.Stloc, local);

            var i = _il.DeclareLocal(typeof(int));
            _il.Emit(OpCodes.Ldc_I4_0);
            _il.Emit(OpCodes.Stloc, i);

            var loopBody = _il.DefineLabel();
            var loopCond = _il.DefineLabel();
            _il.Emit(OpCodes.Br, loopCond);
            _il.MarkLabel(loopBody);
            _il.Emit(OpCodes.Ldloc, local);
            _il.Emit(OpCodes.Ldloc, i);
            _il.Emit(OpCodes.Ldc_I4, innerLen);
            _il.Emit(OpCodes.Newarr, elemClr);
            _il.Emit(OpCodes.Stelem_Ref);
            _il.Emit(OpCodes.Ldloc, i);
            _il.Emit(OpCodes.Ldc_I4_1);
            _il.Emit(OpCodes.Add);
            _il.Emit(OpCodes.Stloc, i);
            _il.MarkLabel(loopCond);
            _il.Emit(OpCodes.Ldloc, i);
            _il.Emit(OpCodes.Ldc_I4, outerLen);
            _il.Emit(OpCodes.Clt);
            _il.Emit(OpCodes.Brtrue, loopBody);

            _symbols[v.Name] = new VarSlot(v.Array.ElementType, SlotKind.Local, local, -1, false, v.Array);
        }
        else if (v.Array is not null)
        {
            var elemClr = ClrType(v.Array.ElementType);
            var local = _il.DeclareLocal(elemClr.MakeArrayType());
            _il.Emit(OpCodes.Ldc_I4, v.Array.High - v.Array.Low + 1);
            _il.Emit(OpCodes.Newarr, elemClr);
            _il.Emit(OpCodes.Stloc, local);
            _symbols[v.Name] = new VarSlot(v.Array.ElementType, SlotKind.Local, local, -1, false, v.Array);
        }
        else
        {
            var local = _il.DeclareLocal(ClrType(v.Type));
            _symbols[v.Name] = new VarSlot(v.Type, SlotKind.Local, local, -1);
        }
    }

    private static bool IsNumeric(PascalType t) => t is PascalType.Integer or PascalType.Real;

    private static string TypeName(PascalType t) => t switch
    {
        PascalType.Integer => "integer",
        PascalType.Real => "real",
        PascalType.Boolean => "boolean",
        PascalType.StringT => "string",
        PascalType.Void => "record",
        _ => t.ToString(),
    };

    private VarSlot LookupVar(string name, int line, int col)
    {
        if (!_symbols.TryGetValue(name, out var sym))
            throw new SemanticError($"variable '{name}' no declarada", line, col);
        return sym;
    }

    // Pushes the address of a slot's storage (used to pass 'var' arguments and to
    // read/write through a 'var' parameter, which the CLR represents as a byref pointer).
    private void EmitAddress(VarSlot slot)
    {
        if (slot.IsByRef) { _il.Emit(OpCodes.Ldarg, (short)slot.ArgIndex); return; } // already a pointer
        if (slot.Kind == SlotKind.Arg) _il.Emit(OpCodes.Ldarga, (short)slot.ArgIndex);
        else _il.Emit(OpCodes.Ldloca, slot.Local!);
    }

    private void EmitIndirectLoad(PascalType t)
    {
        _il.Emit(t switch
        {
            PascalType.Integer => OpCodes.Ldind_I4,
            PascalType.Boolean => OpCodes.Ldind_I1,
            PascalType.Real => OpCodes.Ldind_R8,
            PascalType.StringT => OpCodes.Ldind_Ref,
            _ => throw new InvalidOperationException($"tipo no soportado: {t}"),
        });
    }

    private void EmitIndirectStore(PascalType t)
    {
        _il.Emit(t switch
        {
            PascalType.Integer => OpCodes.Stind_I4,
            PascalType.Boolean => OpCodes.Stind_I1,
            PascalType.Real => OpCodes.Stind_R8,
            PascalType.StringT => OpCodes.Stind_Ref,
            _ => throw new InvalidOperationException($"tipo no soportado: {t}"),
        });
    }

    private void EmitLdelem(PascalType t)
    {
        _il.Emit(t switch
        {
            PascalType.Integer => OpCodes.Ldelem_I4,
            PascalType.Boolean => OpCodes.Ldelem_I1,
            PascalType.Real => OpCodes.Ldelem_R8,
            PascalType.StringT => OpCodes.Ldelem_Ref,
            _ => throw new InvalidOperationException($"tipo no soportado: {t}"),
        });
    }

    private void EmitStelem(PascalType t)
    {
        _il.Emit(t switch
        {
            PascalType.Integer => OpCodes.Stelem_I4,
            PascalType.Boolean => OpCodes.Stelem_I1,
            PascalType.Real => OpCodes.Stelem_R8,
            PascalType.StringT => OpCodes.Stelem_Ref,
            _ => throw new InvalidOperationException($"tipo no soportado: {t}"),
        });
    }

    // Pushes the address of any addressable expression (variable or array element),
    // used to pass 'var' arguments whose target may itself be an array element.
    private void EmitLvalueAddress(Expr e)
    {
        switch (e)
        {
            case VarExpr v:
                EmitAddress(LookupVar(v.Name, v.Line, v.Col));
                return;
            case IndexExpr ix:
            {
                var slot = LookupVar(ix.ArrayName, ix.Line, ix.Col);
                if (slot.Array is null)
                    throw new SemanticError($"'{ix.ArrayName}' no es un array", ix.Line, ix.Col);
                EmitArrayElementSlot(slot, ix.Indices, ix.Line, ix.Col);
                _il.Emit(OpCodes.Ldelema, ClrType(slot.Array.ElementType));
                return;
            }
            default:
                throw new InvalidOperationException("no es una variable direccionable");
        }
    }

    // Pushes [array reference, zero-based index] for an element access, resolving the
    // intermediate Ldelem_Ref hop for a 2D (jagged-array-backed) access. The caller then
    // emits Ldelem/Stelem/Ldelema for the element type.
    private void EmitArrayElementSlot(VarSlot slot, List<Expr> indices, int line, int col)
    {
        var array = slot.Array!;
        int expectedDims = array.Is2D ? 2 : 1;
        if (indices.Count != expectedDims)
            throw new SemanticError($"se esperaban {expectedDims} índice(s), se dieron {indices.Count}", line, col);
        foreach (var idx in indices)
        {
            if (TypeOf(idx) != PascalType.Integer)
                throw new SemanticError("el índice de un array debe ser integer", line, col);
        }

        EmitLoad(slot); // outer array reference
        EmitExpr(indices[0]);
        if (array.Low != 0) { _il.Emit(OpCodes.Ldc_I4, array.Low); _il.Emit(OpCodes.Sub); }

        if (array.Is2D)
        {
            _il.Emit(OpCodes.Ldelem_Ref); // -> inner array reference
            EmitExpr(indices[1]);
            if (array.Low2!.Value != 0) { _il.Emit(OpCodes.Ldc_I4, array.Low2.Value); _il.Emit(OpCodes.Sub); }
        }
    }

    private PascalType TypeOfLvalue(Expr e) => e switch
    {
        VarExpr v => TypeOfVar(v),
        IndexExpr ix => TypeOfIndex(ix),
        _ => throw new InvalidOperationException("no es una variable direccionable"),
    };

    // A bare variable name only denotes a plain scalar value; using an array or record
    // by name (without an index / field) is a distinct kind of expression in this language.
    private PascalType TypeOfVar(VarExpr v)
    {
        if (!_symbols.ContainsKey(v.Name) && _currentClass is not null && _currentClass.Fields.TryGetValue(v.Name, out var implicitField))
            return implicitField.Type; // unqualified field access inside a method body (implicit Self.name)

        var slot = LookupVar(v.Name, v.Line, v.Col);
        if (slot.RecordType is not null)
            throw new SemanticError($"'{v.Name}' es un record; usa '{v.Name}.campo' para acceder a sus campos", v.Line, v.Col);
        if (slot.ClassType is not null)
            throw new SemanticError($"'{v.Name}' es un objeto; usa '{v.Name}.campo' o '{v.Name}.Metodo()'", v.Line, v.Col);
        if (slot.Array is not null)
            throw new SemanticError($"'{v.Name}' es un array; usa '{v.Name}[indice]' para acceder a sus elementos", v.Line, v.Col);
        return slot.Type;
    }

    private void EmitLoad(VarSlot slot)
    {
        if (slot.IsByRef)
        {
            EmitAddress(slot);
            EmitIndirectLoad(slot.Type);
            return;
        }
        if (slot.Kind == SlotKind.Arg) _il.Emit(OpCodes.Ldarg, (short)slot.ArgIndex);
        else _il.Emit(OpCodes.Ldloc, slot.Local!);
    }

    // Stores a value into a slot. emitValue must leave exactly one value of the slot's
    // type on the stack; ordering differs for byref targets (address must precede value).
    private void EmitStoreTo(VarSlot slot, Action emitValue)
    {
        if (slot.IsByRef)
        {
            EmitAddress(slot);
            emitValue();
            EmitIndirectStore(slot.Type);
            return;
        }
        emitValue();
        if (slot.Kind == SlotKind.Arg) _il.Emit(OpCodes.Starg, (short)slot.ArgIndex);
        else _il.Emit(OpCodes.Stloc, slot.Local!);
    }

    // ---- Pure type inference (no IL emission), used to decide promotions ahead of emission ----
    private PascalType TypeOf(Expr e) => e switch
    {
        IntLiteralExpr => PascalType.Integer,
        RealLiteralExpr => PascalType.Real,
        StringLiteralExpr => PascalType.StringT,
        BoolLiteralExpr => PascalType.Boolean,
        VarExpr v => TypeOfVar(v),
        IndexExpr ix => TypeOfIndex(ix),
        FieldAccessExpr fa => TypeOfFieldAccess(fa),
        UnaryExpr u => TypeOfUnary(u),
        BinaryExpr b => TypeOfBinary(b),
        FuncCallExpr f => TryTypeOfBuiltin(f, out var bt) ? bt : TypeOfCall(f),
        QualifiedCallExpr qc => TypeOfQualified(qc),
        _ => throw new InvalidOperationException("expresión no soportada"),
    };

    // obj.Metodo() used as a value (must return a scalar). TClase.Create() is only valid
    // directly on the right-hand side of a class-typed assignment (see EmitAssign), so
    // reaching it here means it wasn't used that way.
    private PascalType TypeOfQualified(QualifiedCallExpr qc)
    {
        if (_classTypes.ContainsKey(qc.Target))
            throw new SemanticError(
                $"'{qc.Target}.{qc.Member}' solo puede usarse para inicializar una variable de tipo '{qc.Target}' (ej. 'obj := {qc.Target}.Create()')", qc.Line, qc.Col);

        var slot = LookupVar(qc.Target, qc.Line, qc.Col);
        if (slot.ClassType is null)
            throw new SemanticError($"'{qc.Target}' no es un objeto", qc.Line, qc.Col);

        var classInfo = _classTypes[slot.ClassType];
        if (!classInfo.Methods.TryGetValue(qc.Member, out var method))
            throw new SemanticError($"'{slot.ClassType}' no tiene un método '{qc.Member}'", qc.Line, qc.Col);
        CheckAccessible(slot.ClassType, method.IsPrivate, qc.Member, qc.Line, qc.Col);
        if (!method.ReturnType.HasValue)
            throw new SemanticError($"'{qc.Member}' es un procedimiento y no puede usarse como expresión", qc.Line, qc.Col);

        CheckMethodArgs(slot.ClassType, qc.Member, method.Params, qc.Args, qc.Line, qc.Col);
        return method.ReturnType.Value;
    }

    private void CheckMethodArgs(string className, string methodName, List<ParamDecl> parameters, List<Expr> args, int line, int col)
    {
        if (args.Count != parameters.Count)
            throw new SemanticError($"'{className}.{methodName}' espera {parameters.Count} argumento(s), se dieron {args.Count}", line, col);
        for (int i = 0; i < args.Count; i++)
        {
            var param = parameters[i];
            var argType = TypeOf(args[i]);
            bool promote = param.Type == PascalType.Real && argType == PascalType.Integer;
            if (!promote && argType != param.Type)
                throw new SemanticError($"el argumento {i + 1} de '{className}.{methodName}' debe ser {TypeName(param.Type)}, se dio {TypeName(argType)}", line, col);
        }
    }

    // A private field/method is only accessible from inside a method of the same class
    // that declares it (no protected/friend-class nuance — just "am I inside className?").
    private void CheckAccessible(string className, bool isPrivate, string memberName, int line, int col)
    {
        if (!isPrivate) return;
        if (_currentClass is null || !string.Equals(_currentClass.Type.Name, className, StringComparison.OrdinalIgnoreCase))
            throw new SemanticError($"'{memberName}' es privado en '{className}' y no es accesible desde aquí", line, col);
    }

    // Resolves a field regardless of whether the slot holds a record or a class instance.
    private (FieldBuilder Field, PascalType Type, bool IsPrivate) ResolveField(VarSlot slot, string fieldName, string varName, int line, int col)
    {
        if (slot.RecordType is not null)
        {
            var rec = _recordTypes[slot.RecordType];
            if (!rec.Fields.TryGetValue(fieldName, out var recField))
                throw new SemanticError($"'{varName}' no tiene un campo '{fieldName}'", line, col);
            return (recField.Field, recField.Type, false); // records have no privacy
        }
        if (slot.ClassType is not null)
        {
            var cls = _classTypes[slot.ClassType];
            if (!cls.Fields.TryGetValue(fieldName, out var field))
                throw new SemanticError($"'{varName}' no tiene un campo '{fieldName}'", line, col);
            CheckAccessible(slot.ClassType, field.IsPrivate, fieldName, line, col);
            return field;
        }
        throw new SemanticError($"'{varName}' no es un record ni un objeto", line, col);
    }

    private PascalType TypeOfFieldAccess(FieldAccessExpr fa)
    {
        var slot = LookupVar(fa.RecordVarName, fa.Line, fa.Col);
        return ResolveField(slot, fa.FieldName, fa.RecordVarName, fa.Line, fa.Col).Type;
    }

    private void RequireArgs(FuncCallExpr f, int count)
    {
        if (f.Args.Count != count)
            throw new SemanticError($"'{f.Name}' espera {count} argumento(s), se dieron {f.Args.Count}", f.Line, f.Col);
    }

    private void RequireArgType(FuncCallExpr f, int index, PascalType expected)
    {
        var t = TypeOf(f.Args[index]);
        if (t != expected)
            throw new SemanticError($"el argumento {index + 1} de '{f.Name}' debe ser {TypeName(expected)}, se dio {TypeName(t)}", f.Line, f.Col);
    }

    private void RequireArgs(ProcCallStmt call, int count)
    {
        if (call.Args.Count != count)
            throw new SemanticError($"'{call.Name}' espera {count} argumento(s), se dieron {call.Args.Count}", call.Line, call.Col);
    }

    private void RequireArgType(ProcCallStmt call, int index, PascalType expected)
    {
        var t = TypeOf(call.Args[index]);
        if (t != expected)
            throw new SemanticError($"el argumento {index + 1} de '{call.Name}' debe ser {TypeName(expected)}, se dio {TypeName(t)}", call.Line, call.Col);
    }

    // Statement-only builtins (no return value); dispatched from EmitProcCall before
    // falling back to user-declared procedures.
    private bool TryEmitBuiltinProc(ProcCallStmt call)
    {
        if (!BuiltinProcNames.Contains(call.Name)) return false;

        switch (call.Name.ToLowerInvariant())
        {
            case "httpstart":
                RequireArgs(call, 1); RequireArgType(call, 0, PascalType.Integer);
                EmitExpr(call.Args[0]);
                _il.Emit(OpCodes.Call, HttpStartMethod);
                break;
            case "httpsetstatus":
                RequireArgs(call, 1); RequireArgType(call, 0, PascalType.Integer);
                EmitExpr(call.Args[0]);
                _il.Emit(OpCodes.Call, HttpSetStatusMethod);
                break;
            case "httpsetheader":
                RequireArgs(call, 2); RequireArgType(call, 0, PascalType.StringT); RequireArgType(call, 1, PascalType.StringT);
                EmitExpr(call.Args[0]);
                EmitExpr(call.Args[1]);
                _il.Emit(OpCodes.Call, HttpSetHeaderMethod);
                break;
            case "httpwrite":
                RequireArgs(call, 1); RequireArgType(call, 0, PascalType.StringT);
                EmitExpr(call.Args[0]);
                _il.Emit(OpCodes.Call, HttpWriteMethod);
                break;
            case "httpend":
                RequireArgs(call, 0);
                _il.Emit(OpCodes.Call, HttpEndMethod);
                break;
        }
        return true;
    }

    private bool TryTypeOfBuiltin(FuncCallExpr f, out PascalType type)
    {
        if (!BuiltinNames.Contains(f.Name)) { type = default; return false; }

        switch (f.Name.ToLowerInvariant())
        {
            case "length":
                RequireArgs(f, 1); RequireArgType(f, 0, PascalType.StringT);
                type = PascalType.Integer; return true;
            case "copy":
                RequireArgs(f, 3); RequireArgType(f, 0, PascalType.StringT); RequireArgType(f, 1, PascalType.Integer); RequireArgType(f, 2, PascalType.Integer);
                type = PascalType.StringT; return true;
            case "uppercase":
            case "lowercase":
            case "trim":
                RequireArgs(f, 1); RequireArgType(f, 0, PascalType.StringT);
                type = PascalType.StringT; return true;
            case "inttostr":
                RequireArgs(f, 1); RequireArgType(f, 0, PascalType.Integer);
                type = PascalType.StringT; return true;
            case "floattostr":
                RequireArgs(f, 1); RequireArgType(f, 0, PascalType.Real);
                type = PascalType.StringT; return true;
            case "strtoint":
                RequireArgs(f, 1); RequireArgType(f, 0, PascalType.StringT);
                type = PascalType.Integer; return true;
            case "strtofloat":
                RequireArgs(f, 1); RequireArgType(f, 0, PascalType.StringT);
                type = PascalType.Real; return true;
            case "httpwait":
                RequireArgs(f, 0);
                type = PascalType.Boolean; return true;
            case "httpmethod":
            case "httppath":
            case "httpbody":
                RequireArgs(f, 0);
                type = PascalType.StringT; return true;
            case "httpquery":
            case "httpparam":
                RequireArgs(f, 1); RequireArgType(f, 0, PascalType.StringT);
                type = PascalType.StringT; return true;
            case "httpmatch":
                RequireArgs(f, 1); RequireArgType(f, 0, PascalType.StringT);
                type = PascalType.Boolean; return true;
            case "jsongetstring":
                RequireArgs(f, 2); RequireArgType(f, 0, PascalType.StringT); RequireArgType(f, 1, PascalType.StringT);
                type = PascalType.StringT; return true;
            case "jsongetint":
                RequireArgs(f, 2); RequireArgType(f, 0, PascalType.StringT); RequireArgType(f, 1, PascalType.StringT);
                type = PascalType.Integer; return true;
            case "jsonescape":
                RequireArgs(f, 1); RequireArgType(f, 0, PascalType.StringT);
                type = PascalType.StringT; return true;
            case "httpheader":
                RequireArgs(f, 1); RequireArgType(f, 0, PascalType.StringT);
                type = PascalType.StringT; return true;
            case "httpbearertoken":
                RequireArgs(f, 0);
                type = PascalType.StringT; return true;
            case "jwtsign":
                RequireArgs(f, 2); RequireArgType(f, 0, PascalType.StringT); RequireArgType(f, 1, PascalType.StringT);
                type = PascalType.StringT; return true;
            case "jwtverify":
                RequireArgs(f, 2); RequireArgType(f, 0, PascalType.StringT); RequireArgType(f, 1, PascalType.StringT);
                type = PascalType.Boolean; return true;
            case "jwtpayload":
                RequireArgs(f, 1); RequireArgType(f, 0, PascalType.StringT);
                type = PascalType.StringT; return true;
            case "jwtnow":
                RequireArgs(f, 0);
                type = PascalType.Integer; return true;
            case "hashpassword":
                RequireArgs(f, 1); RequireArgType(f, 0, PascalType.StringT);
                type = PascalType.StringT; return true;
            case "verifypassword":
                RequireArgs(f, 2); RequireArgType(f, 0, PascalType.StringT); RequireArgType(f, 1, PascalType.StringT);
                type = PascalType.Boolean; return true;
            default:
                type = default; return false;
        }
    }

    private bool TryEmitBuiltin(FuncCallExpr f, out PascalType type)
    {
        if (!TryTypeOfBuiltin(f, out type)) return false;

        switch (f.Name.ToLowerInvariant())
        {
            case "length":
                EmitExpr(f.Args[0]);
                _il.Emit(OpCodes.Callvirt, StringLengthGet);
                break;
            case "copy":
                EmitExpr(f.Args[0]);
                EmitExpr(f.Args[1]);
                _il.Emit(OpCodes.Ldc_I4_1);
                _il.Emit(OpCodes.Sub);
                EmitExpr(f.Args[2]);
                _il.Emit(OpCodes.Callvirt, StringSubstring);
                break;
            case "uppercase":
                EmitExpr(f.Args[0]);
                _il.Emit(OpCodes.Callvirt, StringToUpper);
                break;
            case "lowercase":
                EmitExpr(f.Args[0]);
                _il.Emit(OpCodes.Callvirt, StringToLower);
                break;
            case "trim":
                EmitExpr(f.Args[0]);
                _il.Emit(OpCodes.Callvirt, StringTrim);
                break;
            case "inttostr":
                EmitExpr(f.Args[0]);
                _il.Emit(OpCodes.Call, ConvertIntToStr);
                break;
            case "floattostr":
                EmitExpr(f.Args[0]);
                _il.Emit(OpCodes.Call, ConvertDoubleToStr);
                break;
            case "strtoint":
                EmitExpr(f.Args[0]);
                _il.Emit(OpCodes.Call, ConvertStrToInt);
                break;
            case "strtofloat":
                EmitExpr(f.Args[0]);
                _il.Emit(OpCodes.Call, ConvertStrToDouble);
                break;
            case "httpwait":
                _il.Emit(OpCodes.Call, HttpWaitMethod);
                break;
            case "httpmethod":
                _il.Emit(OpCodes.Call, HttpMethodMethod);
                break;
            case "httppath":
                _il.Emit(OpCodes.Call, HttpPathMethod);
                break;
            case "httpbody":
                _il.Emit(OpCodes.Call, HttpBodyMethod);
                break;
            case "httpquery":
                EmitExpr(f.Args[0]);
                _il.Emit(OpCodes.Call, HttpQueryMethod);
                break;
            case "httpmatch":
                EmitExpr(f.Args[0]);
                _il.Emit(OpCodes.Call, HttpMatchMethod);
                break;
            case "httpparam":
                EmitExpr(f.Args[0]);
                _il.Emit(OpCodes.Call, HttpParamMethod);
                break;
            case "jsongetstring":
                EmitExpr(f.Args[0]);
                EmitExpr(f.Args[1]);
                _il.Emit(OpCodes.Call, JsonGetStringMethod);
                break;
            case "jsongetint":
                EmitExpr(f.Args[0]);
                EmitExpr(f.Args[1]);
                _il.Emit(OpCodes.Call, JsonGetIntMethod);
                break;
            case "jsonescape":
                EmitExpr(f.Args[0]);
                _il.Emit(OpCodes.Call, JsonEscapeMethod);
                break;
            case "httpheader":
                EmitExpr(f.Args[0]);
                _il.Emit(OpCodes.Call, HttpHeaderMethod);
                break;
            case "httpbearertoken":
                _il.Emit(OpCodes.Call, HttpBearerTokenMethod);
                break;
            case "jwtsign":
                EmitExpr(f.Args[0]);
                EmitExpr(f.Args[1]);
                _il.Emit(OpCodes.Call, JwtSignMethod);
                break;
            case "jwtverify":
                EmitExpr(f.Args[0]);
                EmitExpr(f.Args[1]);
                _il.Emit(OpCodes.Call, JwtVerifyMethod);
                break;
            case "jwtpayload":
                EmitExpr(f.Args[0]);
                _il.Emit(OpCodes.Call, JwtPayloadMethod);
                break;
            case "jwtnow":
                _il.Emit(OpCodes.Call, JwtNowMethod);
                break;
            case "hashpassword":
                EmitExpr(f.Args[0]);
                _il.Emit(OpCodes.Call, HashPasswordMethod);
                break;
            case "verifypassword":
                EmitExpr(f.Args[0]);
                EmitExpr(f.Args[1]);
                _il.Emit(OpCodes.Call, VerifyPasswordMethod);
                break;
        }
        return true;
    }

    private PascalType TypeOfIndex(IndexExpr ix)
    {
        var slot = LookupVar(ix.ArrayName, ix.Line, ix.Col);
        if (slot.Array is null)
            throw new SemanticError($"'{ix.ArrayName}' no es un array", ix.Line, ix.Col);
        int expectedDims = slot.Array.Is2D ? 2 : 1;
        if (ix.Indices.Count != expectedDims)
            throw new SemanticError($"'{ix.ArrayName}' tiene {expectedDims} dimensión(es), se dieron {ix.Indices.Count} índice(s)", ix.Line, ix.Col);
        foreach (var idx in ix.Indices)
        {
            if (TypeOf(idx) != PascalType.Integer)
                throw new SemanticError("el índice de un array debe ser integer", ix.Line, ix.Col);
        }
        return slot.Array.ElementType;
    }

    private PascalType TypeOfCall(FuncCallExpr f)
    {
        if (!_functions.TryGetValue(f.Name, out var info))
            throw new SemanticError($"función '{f.Name}' no declarada", f.Line, f.Col);
        if (!info.IsFunction)
            throw new SemanticError($"'{f.Name}' es un procedimiento y no puede usarse como expresión", f.Line, f.Col);
        CheckCallArgs(f.Name, info, f.Args, f.Line, f.Col);
        return info.ReturnType ?? PascalType.Void; // Void is the record-return sentinel here
    }

    private void CheckCallArgs(string name, FuncInfo info, List<Expr> args, int line, int col)
    {
        if (args.Count != info.Params.Count)
            throw new SemanticError($"'{name}' espera {info.Params.Count} argumento(s), se dieron {args.Count}", line, col);
        for (int i = 0; i < args.Count; i++)
        {
            var param = info.Params[i];

            if (param.RecordType is not null)
            {
                // param.RecordType names either a record or a class; either kind of variable is fine.
                if (args[i] is not VarExpr ve)
                    throw new SemanticError($"el argumento {i + 1} de '{name}' debe ser una variable de tipo '{param.RecordType}'", line, col);
                var argSlot = LookupVar(ve.Name, ve.Line, ve.Col);
                if (argSlot.RecordType != param.RecordType && argSlot.ClassType != param.RecordType)
                    throw new SemanticError($"el argumento {i + 1} de '{name}' debe ser de tipo '{param.RecordType}'", line, col);
                continue;
            }

            if (param.Array is not null)
            {
                if (args[i] is not VarExpr ve)
                    throw new SemanticError($"el argumento {i + 1} de '{name}' debe ser una variable array", line, col);
                var argSlot = LookupVar(ve.Name, ve.Line, ve.Col);
                if (argSlot.Array is null || argSlot.Array.ElementType != param.Array.ElementType)
                    throw new SemanticError($"el argumento {i + 1} de '{name}' debe ser un array de {TypeName(param.Array.ElementType)}", line, col);
                continue;
            }

            if (param.ByRef)
            {
                if (args[i] is not (VarExpr or IndexExpr))
                    throw new SemanticError($"el argumento {i + 1} de '{name}' es 'var' y requiere pasar una variable o un elemento de array, no una expresión", line, col);
                var lvalueType = TypeOfLvalue(args[i]);
                if (lvalueType != param.Type)
                    throw new SemanticError(
                        $"el argumento {i + 1} de '{name}' debe ser exactamente {TypeName(param.Type)} (parámetro 'var'), se dio {TypeName(lvalueType)}", line, col);
                continue;
            }

            var argType = TypeOf(args[i]);
            bool promote = param.Type == PascalType.Real && argType == PascalType.Integer;
            if (!promote && argType != param.Type)
                throw new SemanticError(
                    $"el argumento {i + 1} de '{name}' debe ser {TypeName(param.Type)}, se dio {TypeName(argType)}", line, col);
        }
    }

    // Validates and emits IL for a call's arguments; must run after the callee is known to exist.
    private void EmitArgs(string name, FuncInfo info, List<Expr> args, int line, int col)
    {
        CheckCallArgs(name, info, args, line, col);
        for (int i = 0; i < args.Count; i++)
        {
            var param = info.Params[i];
            if (param.RecordType is not null)
            {
                var ve = (VarExpr)args[i];
                EmitLoad(LookupVar(ve.Name, ve.Line, ve.Col)); // record reference, passed as-is
                continue;
            }
            if (param.Array is not null)
            {
                var ve = (VarExpr)args[i];
                EmitLoad(LookupVar(ve.Name, ve.Line, ve.Col)); // array reference, passed as-is
                continue;
            }
            if (param.ByRef)
            {
                EmitLvalueAddress(args[i]);
                continue;
            }

            var argType = TypeOf(args[i]);
            EmitPromoted(args[i], argType, param.Type == PascalType.Real);
        }
    }

    private PascalType TypeOfUnary(UnaryExpr u)
    {
        var t = TypeOf(u.Operand);
        if (u.Op == TokenType.Not)
        {
            if (t != PascalType.Boolean)
                throw new SemanticError("'not' requiere un operando booleano", u.Line, u.Col);
            return PascalType.Boolean;
        }
        // Minus
        if (!IsNumeric(t))
            throw new SemanticError("'-' unario requiere un operando numérico", u.Line, u.Col);
        return t;
    }

    private PascalType TypeOfBinary(BinaryExpr b)
    {
        var lt = TypeOf(b.Left);
        var rt = TypeOf(b.Right);
        switch (b.Op)
        {
            case TokenType.Plus:
                if (lt == PascalType.StringT && rt == PascalType.StringT) return PascalType.StringT;
                if (IsNumeric(lt) && IsNumeric(rt)) return lt == PascalType.Real || rt == PascalType.Real ? PascalType.Real : PascalType.Integer;
                throw new SemanticError("tipos incompatibles para '+'", b.Line, b.Col);
            case TokenType.Minus:
            case TokenType.Star:
                if (IsNumeric(lt) && IsNumeric(rt)) return lt == PascalType.Real || rt == PascalType.Real ? PascalType.Real : PascalType.Integer;
                throw new SemanticError($"tipos incompatibles para '{b.Op}'", b.Line, b.Col);
            case TokenType.Slash:
                if (IsNumeric(lt) && IsNumeric(rt)) return PascalType.Real;
                throw new SemanticError("tipos incompatibles para '/'", b.Line, b.Col);
            case TokenType.Div:
            case TokenType.Mod:
                if (lt == PascalType.Integer && rt == PascalType.Integer) return PascalType.Integer;
                throw new SemanticError("'div'/'mod' requieren operandos enteros", b.Line, b.Col);
            case TokenType.And:
            case TokenType.Or:
                if (lt == PascalType.Boolean && rt == PascalType.Boolean) return PascalType.Boolean;
                throw new SemanticError("'and'/'or' requieren operandos booleanos", b.Line, b.Col);
            case TokenType.Eq:
            case TokenType.Neq:
                if (IsNumeric(lt) && IsNumeric(rt)) return PascalType.Boolean;
                if (lt == rt && (lt == PascalType.StringT || lt == PascalType.Boolean)) return PascalType.Boolean;
                throw new SemanticError("tipos incompatibles para comparación", b.Line, b.Col);
            case TokenType.Lt:
            case TokenType.Gt:
            case TokenType.Le:
            case TokenType.Ge:
                if (IsNumeric(lt) && IsNumeric(rt)) return PascalType.Boolean;
                throw new SemanticError("el operador relacional requiere operandos numéricos", b.Line, b.Col);
            default:
                throw new InvalidOperationException($"operador no soportado: {b.Op}");
        }
    }

    // ---- Emission ----
    private PascalType EmitExpr(Expr e)
    {
        switch (e)
        {
            case IntLiteralExpr lit:
                _il.Emit(OpCodes.Ldc_I4, lit.Value);
                return PascalType.Integer;
            case RealLiteralExpr lit:
                _il.Emit(OpCodes.Ldc_R8, lit.Value);
                return PascalType.Real;
            case StringLiteralExpr lit:
                _il.Emit(OpCodes.Ldstr, lit.Value);
                return PascalType.StringT;
            case BoolLiteralExpr lit:
                _il.Emit(lit.Value ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
                return PascalType.Boolean;
            case VarExpr v:
            {
                var type = TypeOfVar(v); // validates it's a plain scalar, not an array/record/object
                if (!_symbols.ContainsKey(v.Name) && _currentClass is not null)
                {
                    _il.Emit(OpCodes.Ldarg_0); // Self
                    _il.Emit(OpCodes.Ldfld, _currentClass.Fields[v.Name].Field);
                    return type;
                }
                EmitLoad(LookupVar(v.Name, v.Line, v.Col));
                return type;
            }
            case IndexExpr ix:
                return EmitIndexLoad(ix);
            case FieldAccessExpr fa:
                return EmitFieldAccess(fa);
            case UnaryExpr u:
                return EmitUnary(u);
            case BinaryExpr b:
                return EmitBinary(b);
            case FuncCallExpr f:
                return TryEmitBuiltin(f, out var bt) ? bt : EmitCall(f);
            case QualifiedCallExpr qc:
                return EmitQualifiedCallExpr(qc);
            default:
                throw new InvalidOperationException("expresión no soportada");
        }
    }

    private PascalType EmitQualifiedCallExpr(QualifiedCallExpr qc)
    {
        var type = TypeOfQualified(qc); // validates
        var slot = LookupVar(qc.Target, qc.Line, qc.Col);
        var classInfo = _classTypes[slot.ClassType!];
        var method = classInfo.Methods[qc.Member];

        EmitLoad(slot); // Self
        for (int i = 0; i < qc.Args.Count; i++)
        {
            var argType = TypeOf(qc.Args[i]);
            EmitPromoted(qc.Args[i], argType, method.Params[i].Type == PascalType.Real);
        }
        _il.Emit(OpCodes.Callvirt, method.Method);
        return type;
    }

    private PascalType EmitFieldAccess(FieldAccessExpr fa)
    {
        var type = TypeOfFieldAccess(fa); // validates
        var slot = LookupVar(fa.RecordVarName, fa.Line, fa.Col);
        var field = ResolveField(slot, fa.FieldName, fa.RecordVarName, fa.Line, fa.Col);
        EmitLoad(slot); // record/object reference
        _il.Emit(OpCodes.Ldfld, field.Field);
        return type;
    }

    private PascalType EmitIndexLoad(IndexExpr ix)
    {
        var elemType = TypeOfIndex(ix); // validates and resolves element type
        var slot = LookupVar(ix.ArrayName, ix.Line, ix.Col);

        EmitArrayElementSlot(slot, ix.Indices, ix.Line, ix.Col);
        EmitLdelem(elemType);
        return elemType;
    }

    private PascalType EmitCall(FuncCallExpr f)
    {
        if (!_functions.TryGetValue(f.Name, out var info))
            throw new SemanticError($"función '{f.Name}' no declarada", f.Line, f.Col);
        if (!info.IsFunction)
            throw new SemanticError($"'{f.Name}' es un procedimiento y no puede usarse como expresión", f.Line, f.Col);

        EmitArgs(f.Name, info, f.Args, f.Line, f.Col);
        _il.Emit(OpCodes.Call, info.Method);
        return info.ReturnType ?? PascalType.Void; // Void is the record-return sentinel here
    }

    private PascalType EmitUnary(UnaryExpr u)
    {
        var type = TypeOfUnary(u);
        EmitExpr(u.Operand);
        if (u.Op == TokenType.Not)
        {
            _il.Emit(OpCodes.Ldc_I4_0);
            _il.Emit(OpCodes.Ceq);
        }
        else
        {
            _il.Emit(OpCodes.Neg);
        }
        return type;
    }

    private PascalType EmitBinary(BinaryExpr b)
    {
        var lt = TypeOf(b.Left);
        var rt = TypeOf(b.Right);

        switch (b.Op)
        {
            case TokenType.Plus when lt == PascalType.StringT && rt == PascalType.StringT:
                EmitExpr(b.Left);
                EmitExpr(b.Right);
                _il.Emit(OpCodes.Call, StringConcat);
                return PascalType.StringT;

            case TokenType.Plus:
                return EmitNumericBinary(b, lt, rt, OpCodes.Add);
            case TokenType.Minus:
                return EmitNumericBinary(b, lt, rt, OpCodes.Sub);
            case TokenType.Star:
                return EmitNumericBinary(b, lt, rt, OpCodes.Mul);

            case TokenType.Slash:
                EmitPromoted(b.Left, lt, true);
                EmitPromoted(b.Right, rt, true);
                _il.Emit(OpCodes.Div);
                return PascalType.Real;

            case TokenType.Div:
                EmitExpr(b.Left);
                EmitExpr(b.Right);
                _il.Emit(OpCodes.Div);
                return PascalType.Integer;

            case TokenType.Mod:
                EmitExpr(b.Left);
                EmitExpr(b.Right);
                _il.Emit(OpCodes.Rem);
                return PascalType.Integer;

            case TokenType.And:
                EmitExpr(b.Left);
                EmitExpr(b.Right);
                _il.Emit(OpCodes.And);
                return PascalType.Boolean;

            case TokenType.Or:
                EmitExpr(b.Left);
                EmitExpr(b.Right);
                _il.Emit(OpCodes.Or);
                return PascalType.Boolean;

            case TokenType.Eq:
            case TokenType.Neq:
                EmitEquality(b, lt, rt);
                if (b.Op == TokenType.Neq)
                {
                    _il.Emit(OpCodes.Ldc_I4_0);
                    _il.Emit(OpCodes.Ceq);
                }
                return PascalType.Boolean;

            case TokenType.Lt:
            case TokenType.Gt:
            case TokenType.Le:
            case TokenType.Ge:
                EmitRelational(b, lt, rt);
                return PascalType.Boolean;

            default:
                throw new InvalidOperationException($"operador no soportado: {b.Op}");
        }
    }

    private PascalType EmitNumericBinary(BinaryExpr b, PascalType lt, PascalType rt, OpCode op)
    {
        bool real = lt == PascalType.Real || rt == PascalType.Real;
        EmitPromoted(b.Left, lt, real);
        EmitPromoted(b.Right, rt, real);
        _il.Emit(op);
        return real ? PascalType.Real : PascalType.Integer;
    }

    private void EmitPromoted(Expr e, PascalType actual, bool promoteToReal)
    {
        EmitExpr(e);
        if (promoteToReal && actual == PascalType.Integer)
            _il.Emit(OpCodes.Conv_R8);
    }

    private void EmitEquality(BinaryExpr b, PascalType lt, PascalType rt)
    {
        if (IsNumeric(lt) && IsNumeric(rt))
        {
            bool real = lt == PascalType.Real || rt == PascalType.Real;
            EmitPromoted(b.Left, lt, real);
            EmitPromoted(b.Right, rt, real);
            _il.Emit(OpCodes.Ceq);
            return;
        }

        EmitExpr(b.Left);
        EmitExpr(b.Right);
        if (lt == PascalType.StringT)
            _il.Emit(OpCodes.Call, StringEquals);
        else
            _il.Emit(OpCodes.Ceq);
    }

    private void EmitRelational(BinaryExpr b, PascalType lt, PascalType rt)
    {
        bool real = lt == PascalType.Real || rt == PascalType.Real;
        EmitPromoted(b.Left, lt, real);
        EmitPromoted(b.Right, rt, real);
        switch (b.Op)
        {
            case TokenType.Lt:
                _il.Emit(OpCodes.Clt);
                break;
            case TokenType.Gt:
                _il.Emit(OpCodes.Cgt);
                break;
            case TokenType.Le:
                _il.Emit(OpCodes.Cgt);
                _il.Emit(OpCodes.Ldc_I4_0);
                _il.Emit(OpCodes.Ceq);
                break;
            case TokenType.Ge:
                _il.Emit(OpCodes.Clt);
                _il.Emit(OpCodes.Ldc_I4_0);
                _il.Emit(OpCodes.Ceq);
                break;
        }
    }

    private void EmitStmt(Stmt s)
    {
        switch (s)
        {
            case AssignStmt a:
                EmitAssign(a);
                break;

            case IndexedAssignStmt ia:
                EmitIndexedAssign(ia);
                break;

            case FieldAssignStmt fs:
                EmitFieldAssign(fs);
                break;

            case IfStmt ifs:
                EmitIf(ifs);
                break;

            case WhileStmt w:
                EmitWhile(w);
                break;

            case ForStmt f:
                EmitFor(f);
                break;

            case CaseStmt c:
                EmitCase(c);
                break;

            case CompoundStmt c:
                foreach (var st in c.Statements) EmitStmt(st);
                break;

            case WriteLnStmt wl:
                foreach (var arg in wl.Args)
                {
                    var t = EmitExpr(arg);
                    var writeMethod = typeof(Console).GetMethod("Write", new[] { ClrType(t) })!;
                    _il.Emit(OpCodes.Call, writeMethod);
                }
                if (wl.Newline) _il.Emit(OpCodes.Call, ConsoleWriteLineVoid);
                break;

            case ReadLnStmt:
                _il.Emit(OpCodes.Call, ConsoleReadLine);
                _il.Emit(OpCodes.Pop);
                break;

            case ProcCallStmt call:
                EmitProcCall(call);
                break;

            case QualifiedCallStmt qc:
                EmitQualifiedCallStmt(qc);
                break;

            case EmptyStmt:
                break;

            default:
                throw new InvalidOperationException("instrucción no soportada");
        }
    }

    private void EmitQualifiedCallStmt(QualifiedCallStmt qc)
    {
        if (_classTypes.TryGetValue(qc.Target, out var classInfoForCtor))
        {
            if (!string.Equals(qc.Member, "Create", StringComparison.OrdinalIgnoreCase))
                throw new SemanticError($"solo se soporta '{qc.Target}.Create()' para construir instancias por ahora", qc.Line, qc.Col);
            if (qc.Args.Count != 0)
                throw new SemanticError("'Create' no admite argumentos todavía", qc.Line, qc.Col);
            _il.Emit(OpCodes.Newobj, classInfoForCtor.Ctor);
            _il.Emit(OpCodes.Pop); // discarded; usually you'd assign this to a variable instead
            return;
        }

        var slot = LookupVar(qc.Target, qc.Line, qc.Col);
        if (slot.ClassType is null)
            throw new SemanticError($"'{qc.Target}' no es un objeto", qc.Line, qc.Col);

        var classInfo = _classTypes[slot.ClassType];
        if (!classInfo.Methods.TryGetValue(qc.Member, out var method))
            throw new SemanticError($"'{slot.ClassType}' no tiene un método '{qc.Member}'", qc.Line, qc.Col);
        CheckAccessible(slot.ClassType, method.IsPrivate, qc.Member, qc.Line, qc.Col);
        if (method.ReturnType.HasValue)
            throw new SemanticError($"'{qc.Member}' es una función; asigne su resultado a una variable en lugar de llamarla como instrucción", qc.Line, qc.Col);

        CheckMethodArgs(slot.ClassType, qc.Member, method.Params, qc.Args, qc.Line, qc.Col);

        EmitLoad(slot); // Self
        for (int i = 0; i < qc.Args.Count; i++)
        {
            var argType = TypeOf(qc.Args[i]);
            EmitPromoted(qc.Args[i], argType, method.Params[i].Type == PascalType.Real);
        }
        _il.Emit(OpCodes.Callvirt, method.Method);
    }

    private void EmitProcCall(ProcCallStmt call)
    {
        if (TryEmitBuiltinProc(call)) return;

        if (!_functions.TryGetValue(call.Name, out var info))
            throw new SemanticError($"'{call.Name}' no declarado", call.Line, call.Col);
        if (info.IsFunction)
            throw new SemanticError($"'{call.Name}' es una función; asigne su resultado a una variable en lugar de llamarla como instrucción", call.Line, call.Col);

        EmitArgs(call.Name, info, call.Args, call.Line, call.Col);
        _il.Emit(OpCodes.Call, info.Method);
    }

    private void EmitAssign(AssignStmt a)
    {
        if (!_symbols.ContainsKey(a.Name) && _currentClass is not null && _currentClass.Fields.TryGetValue(a.Name, out var implicitField))
        {
            // Unqualified assignment inside a method body (implicit Self.name := ...).
            var implicitValType = TypeOf(a.Value);
            bool implicitPromote = implicitField.Type == PascalType.Real && implicitValType == PascalType.Integer;
            if (!implicitPromote && implicitField.Type != implicitValType)
                throw new SemanticError($"no se puede asignar un valor de tipo {TypeName(implicitValType)} al campo '{a.Name}' de tipo {TypeName(implicitField.Type)}", a.Line, a.Col);

            _il.Emit(OpCodes.Ldarg_0); // Self
            EmitExpr(a.Value);
            if (implicitPromote) _il.Emit(OpCodes.Conv_R8);
            _il.Emit(OpCodes.Stfld, implicitField.Field);
            return;
        }

        var sym = LookupVar(a.Name, a.Line, a.Col);
        if (sym.Array is not null)
            throw new SemanticError($"no se puede asignar directamente a un array completo; asigne elemento por elemento ('{a.Name}[i] := ...')", a.Line, a.Col);

        if (sym.RecordType is not null)
        {
            // Records are reference-shared classes here (see arrays_var caveat); whole-record
            // assignment copies the reference, so both variables alias the same fields.
            if (a.Value is VarExpr ve)
            {
                var srcSlot = LookupVar(ve.Name, ve.Line, ve.Col);
                if (srcSlot.RecordType != sym.RecordType)
                    throw new SemanticError($"no se puede asignar '{ve.Name}' a '{a.Name}': son de tipos distintos", a.Line, a.Col);
                EmitStoreTo(sym, () => EmitLoad(srcSlot));
                return;
            }
            if (a.Value is FuncCallExpr fc)
            {
                if (!_functions.TryGetValue(fc.Name, out var info) || info.ReturnRecordType != sym.RecordType)
                    throw new SemanticError($"'{fc.Name}' no devuelve un record de tipo '{sym.RecordType}'", a.Line, a.Col);
                EmitStoreTo(sym, () => EmitCall(fc));
                return;
            }
            throw new SemanticError("solo se puede asignar un record a partir de otra variable o una función que devuelva ese tipo", a.Line, a.Col);
        }

        if (sym.ClassType is not null)
        {
            // Objects are reference types: assignment from a variable aliases the same
            // instance; TClase.Create() constructs a brand-new one.
            if (a.Value is VarExpr ve)
            {
                var srcSlot = LookupVar(ve.Name, ve.Line, ve.Col);
                if (srcSlot.ClassType != sym.ClassType)
                    throw new SemanticError($"no se puede asignar '{ve.Name}' a '{a.Name}': son de tipos distintos", a.Line, a.Col);
                EmitStoreTo(sym, () => EmitLoad(srcSlot));
                return;
            }
            if (a.Value is QualifiedCallExpr qc && string.Equals(qc.Target, sym.ClassType, StringComparison.OrdinalIgnoreCase)
                && string.Equals(qc.Member, "Create", StringComparison.OrdinalIgnoreCase))
            {
                if (qc.Args.Count != 0)
                    throw new SemanticError("'Create' no admite argumentos todavía", a.Line, a.Col);
                EmitStoreTo(sym, () => _il.Emit(OpCodes.Newobj, _classTypes[sym.ClassType].Ctor));
                return;
            }
            throw new SemanticError($"solo se puede asignar un objeto a partir de otra variable del mismo tipo o '{sym.ClassType}.Create()'", a.Line, a.Col);
        }

        var valType = TypeOf(a.Value);

        bool promote = sym.Type == PascalType.Real && valType == PascalType.Integer;
        if (!promote && sym.Type != valType)
            throw new SemanticError($"no se puede asignar un valor de tipo {TypeName(valType)} a la variable '{a.Name}' de tipo {TypeName(sym.Type)}", a.Line, a.Col);

        EmitStoreTo(sym, () =>
        {
            EmitExpr(a.Value);
            if (promote) _il.Emit(OpCodes.Conv_R8);
        });
    }

    private void EmitFieldAssign(FieldAssignStmt s)
    {
        var slot = LookupVar(s.RecordVarName, s.Line, s.Col);
        var field = ResolveField(slot, s.FieldName, s.RecordVarName, s.Line, s.Col);

        var valType = TypeOf(s.Value);
        bool promote = field.Type == PascalType.Real && valType == PascalType.Integer;
        if (!promote && field.Type != valType)
            throw new SemanticError($"no se puede asignar un valor de tipo {TypeName(valType)} al campo '{s.FieldName}' de tipo {TypeName(field.Type)}", s.Line, s.Col);

        EmitLoad(slot); // record/object reference
        EmitExpr(s.Value);
        if (promote) _il.Emit(OpCodes.Conv_R8);
        _il.Emit(OpCodes.Stfld, field.Field);
    }

    private void EmitIndexedAssign(IndexedAssignStmt s)
    {
        var slot = LookupVar(s.ArrayName, s.Line, s.Col);
        if (slot.Array is null)
            throw new SemanticError($"'{s.ArrayName}' no es un array", s.Line, s.Col);

        var elemType = slot.Array.ElementType;
        var valType = TypeOf(s.Value);
        bool promote = elemType == PascalType.Real && valType == PascalType.Integer;
        if (!promote && elemType != valType)
            throw new SemanticError($"no se puede asignar un valor de tipo {TypeName(valType)} a un elemento de tipo {TypeName(elemType)}", s.Line, s.Col);

        EmitArrayElementSlot(slot, s.Indices, s.Line, s.Col);
        EmitExpr(s.Value);
        if (promote) _il.Emit(OpCodes.Conv_R8);
        EmitStelem(elemType);
    }

    private void EmitIf(IfStmt ifs)
    {
        var condType = TypeOf(ifs.Condition);
        if (condType != PascalType.Boolean)
            throw new SemanticError("la condición de 'if' debe ser booleana", ifs.Line, ifs.Col);

        EmitExpr(ifs.Condition);
        var elseLabel = _il.DefineLabel();
        var endLabel = _il.DefineLabel();
        _il.Emit(OpCodes.Brfalse, elseLabel);
        EmitStmt(ifs.Then);
        _il.Emit(OpCodes.Br, endLabel);
        _il.MarkLabel(elseLabel);
        if (ifs.Else is not null) EmitStmt(ifs.Else);
        _il.MarkLabel(endLabel);
    }

    private void EmitWhile(WhileStmt w)
    {
        var condType = TypeOf(w.Condition);
        if (condType != PascalType.Boolean)
            throw new SemanticError("la condición de 'while' debe ser booleana", w.Line, w.Col);

        var startLabel = _il.DefineLabel();
        var endLabel = _il.DefineLabel();
        _il.MarkLabel(startLabel);
        EmitExpr(w.Condition);
        _il.Emit(OpCodes.Brfalse, endLabel);
        EmitStmt(w.Body);
        _il.Emit(OpCodes.Br, startLabel);
        _il.MarkLabel(endLabel);
    }

    private void EmitFor(ForStmt f)
    {
        var loopVar = LookupVar(f.VarName, f.Line, f.Col);
        if (loopVar.Type != PascalType.Integer)
            throw new SemanticError("la variable de un 'for' debe ser integer", f.Line, f.Col);

        var startType = TypeOf(f.Start);
        if (startType != PascalType.Integer)
            throw new SemanticError("el valor inicial de 'for' debe ser integer", f.Line, f.Col);
        var endType = TypeOf(f.End);
        if (endType != PascalType.Integer)
            throw new SemanticError("el valor final de 'for' debe ser integer", f.Line, f.Col);

        EmitStoreTo(loopVar, () => EmitExpr(f.Start));

        var endLocal = _il.DeclareLocal(typeof(int));
        EmitExpr(f.End);
        _il.Emit(OpCodes.Stloc, endLocal);

        var condLabel = _il.DefineLabel();
        var bodyLabel = _il.DefineLabel();
        var endLabel = _il.DefineLabel();

        _il.Emit(OpCodes.Br, condLabel);
        _il.MarkLabel(bodyLabel);
        EmitStmt(f.Body);

        EmitStoreTo(loopVar, () =>
        {
            EmitLoad(loopVar);
            _il.Emit(OpCodes.Ldc_I4_1);
            _il.Emit(f.Down ? OpCodes.Sub : OpCodes.Add);
        });

        _il.MarkLabel(condLabel);
        EmitLoad(loopVar);
        _il.Emit(OpCodes.Ldloc, endLocal);
        _il.Emit(f.Down ? OpCodes.Clt : OpCodes.Cgt); // loopVar past end?
        _il.Emit(OpCodes.Brtrue, endLabel);
        _il.Emit(OpCodes.Br, bodyLabel);

        _il.MarkLabel(endLabel);
    }

    private void EmitCase(CaseStmt cs)
    {
        var selType = TypeOf(cs.Selector);
        if (selType != PascalType.Integer && selType != PascalType.Boolean)
            throw new SemanticError("el selector de 'case' debe ser integer o boolean", cs.Line, cs.Col);

        var selectorLocal = _il.DeclareLocal(ClrType(selType));
        EmitExpr(cs.Selector);
        _il.Emit(OpCodes.Stloc, selectorLocal);

        var endLabel = _il.DefineLabel();

        foreach (var branch in cs.Branches)
        {
            var bodyLabel = _il.DefineLabel();
            var nextBranchLabel = _il.DefineLabel();

            foreach (var label in branch.Labels)
            {
                var labelType = TypeOf(label);
                if (labelType != selType)
                    throw new SemanticError($"la etiqueta de 'case' debe ser de tipo {TypeName(selType)}", cs.Line, cs.Col);

                _il.Emit(OpCodes.Ldloc, selectorLocal);
                EmitExpr(label);
                _il.Emit(OpCodes.Ceq);
                _il.Emit(OpCodes.Brtrue, bodyLabel);
            }
            _il.Emit(OpCodes.Br, nextBranchLabel);

            _il.MarkLabel(bodyLabel);
            EmitStmt(branch.Body);
            _il.Emit(OpCodes.Br, endLabel);

            _il.MarkLabel(nextBranchLabel);
        }

        if (cs.ElseBranch is not null)
            EmitStmt(cs.ElseBranch);

        _il.MarkLabel(endLabel);
    }
}
