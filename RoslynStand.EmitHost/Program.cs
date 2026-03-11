using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;

namespace RoslynStand.EmitHost;

internal static class Program
{
    private static ImmutableArray<MetadataReference> GetMetadataReferences()
    {
        ImmutableArray<PortableExecutableReference>? trustedPlatformAssemblies =
            ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))?
            .Split(Path.PathSeparator)
            .Distinct(StringComparer.Ordinal)
            .Select(path => MetadataReference.CreateFromFile(path))
            .ToImmutableArray();

        return trustedPlatformAssemblies?.Cast<MetadataReference>().ToImmutableArray() ?? [];
    }
    private static int Main(string[] args)
    {
        string sampleProjectPath = args.Length > 0
            ? Path.GetFullPath(args[0])
            : ResolveDefaultSampleProjectPath();

        if (!Directory.Exists(sampleProjectPath))
        {
            Console.Error.WriteLine($"Sample project directory not found: {sampleProjectPath}");
            return 1;
        }

        SyntaxTree[] syntaxTrees = Directory
            .EnumerateFiles(sampleProjectPath, "*.cs", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => CSharpSyntaxTree.ParseText(
                SourceText.From(File.ReadAllText(path), Encoding.UTF8),
                path: path,
                options: new CSharpParseOptions(LanguageVersion.Latest)))
            .ToArray();

        var compilation = CSharpCompilation.Create(
            "RoslynStand.EmittedSample",
            syntaxTrees,
            GetMetadataReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable));

        using var assemblyStream = new MemoryStream();
        using var pdbStream = new MemoryStream();

        EmitResult emitResult = compilation.Emit(assemblyStream, pdbStream);
        if (!emitResult.Success)
        {
            Console.Error.WriteLine("Emit failed:");
            foreach (Diagnostic diagnostic in emitResult.Diagnostics.OrderBy(diagnostic =>
                         diagnostic.Location.SourceSpan.Start))
            {
                Console.Error.WriteLine(diagnostic.ToString());
            }

            return 1;
        }

        assemblyStream.Position = 0;
        pdbStream.Position = 0;

        var loadContext = new AssemblyLoadContext("RoslynStand.EmitHost", true);
        Assembly assembly = loadContext.LoadFromStream(assemblyStream, pdbStream);

        PrintMethodIl(assembly, "RoslynStand.Sample.OrderService", "GetSummaryAsync");
        loadContext.Unload();
        return 0;
    }

    private static void PrintMethodIl(Assembly assembly, string typeName, string methodName)
    {
        Type? type = assembly.GetType(typeName);
        if (type is null)
        {
            Console.Error.WriteLine($"Type '{typeName}' not found.");
            return;
        }

        MethodInfo? method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
        if (method is null)
        {
            Console.Error.WriteLine($"Method '{methodName}' not found.");
            return;
        }

        Console.WriteLine($"IL for {type.FullName}.{method.Name}:");
        foreach (string line in IlDisassembler.Disassemble(method))
        {
            Console.WriteLine(line);
        }

        Type? stateMachineType = method.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType;

        if (stateMachineType is null)
        {
            return;
        }

        MethodInfo? moveNext = stateMachineType.GetMethod(
            "MoveNext",
            BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        if (moveNext is null)
        {
            return;
        }

        Console.WriteLine();
        Console.WriteLine($"IL for {stateMachineType.FullName}.MoveNext:");
        foreach (string line in IlDisassembler.Disassemble(moveNext))
        {
            Console.WriteLine(line);
        }
    }

    private static string ResolveDefaultSampleProjectPath() => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "../../../../RoslynStand.Sample"));
}

internal static class IlDisassembler
{
    private readonly static IReadOnlyDictionary<short, OpCode> SingleByteOpCodes =
        BuildOpCodeMap(opCode => opCode.Size == 1);

    private readonly static IReadOnlyDictionary<short, OpCode> MultiByteOpCodes =
        BuildOpCodeMap(opCode => opCode.Size == 2);

    public static IEnumerable<string> Disassemble(MethodInfo method)
    {
        MethodBody? body = method.GetMethodBody();
        if (body is null)
        {
            yield return "<no method body>";
            yield break;
        }

        byte[]? il = body.GetILAsByteArray();
        if (il is null)
        {
            yield return "<no IL bytes>";
            yield break;
        }

        var offset = 0;
        while (offset < il.Length)
        {
            int instructionOffset = offset;
            OpCode opCode;

            byte first = il[offset++];
            if (first == 0xFE)
            {
                byte second = il[offset++];
                opCode = MultiByteOpCodes[(short)(first << 8 | second)];
            }
            else
            {
                opCode = SingleByteOpCodes[first];
            }

            string operand = ReadOperand(method.Module, il, ref offset, opCode);
            yield return $"{instructionOffset:X4}: {opCode.Name,-12} {operand}".TrimEnd();
        }
    }

    private static IReadOnlyDictionary<short, OpCode> BuildOpCodeMap(Func<OpCode, bool> predicate)
    {
        return typeof(OpCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(OpCode))
            .Select(field => (OpCode)field.GetValue(null)!)
            .Where(predicate)
            .ToDictionary(opCode => opCode.Value);
    }

    private static string ReadOperand(Module module, byte[] il, ref int offset, OpCode opCode)
    {
        switch (opCode.OperandType)
        {
            case OperandType.InlineNone:
                return string.Empty;

            case OperandType.ShortInlineI:
                return il[offset++].ToString();

            case OperandType.InlineI:
                var int32 = BitConverter.ToInt32(il, offset);
                offset += 4;
                return int32.ToString();

            case OperandType.InlineI8:
                var int64 = BitConverter.ToInt64(il, offset);
                offset += 8;
                return int64.ToString();

            case OperandType.ShortInlineR:
                var single = BitConverter.ToSingle(il, offset);
                offset += 4;
                return single.ToString(CultureInfo.InvariantCulture);

            case OperandType.InlineR:
                var @double = BitConverter.ToDouble(il, offset);
                offset += 8;
                return @double.ToString(CultureInfo.InvariantCulture);

            case OperandType.ShortInlineVar:
                return $"V_{il[offset++]}";

            case OperandType.InlineVar:
                var variableIndex = BitConverter.ToUInt16(il, offset);
                offset += 2;
                return $"V_{variableIndex}";

            case OperandType.ShortInlineBrTarget:
                var shortDelta = unchecked((sbyte)il[offset++]);
                return $"IL_{offset + shortDelta:X4}";

            case OperandType.InlineBrTarget:
                var delta = BitConverter.ToInt32(il, offset);
                offset += 4;
                return $"IL_{offset + delta:X4}";

            case OperandType.InlineString:
                var stringToken = BitConverter.ToInt32(il, offset);
                offset += 4;
                return $"\"{module.ResolveString(stringToken)}\"";

            case OperandType.InlineField:
            case OperandType.InlineMethod:
            case OperandType.InlineType:
            case OperandType.InlineTok:
            case OperandType.InlineSig:
                var metadataToken = BitConverter.ToInt32(il, offset);
                offset += 4;
                return ResolveMetadataToken(module, metadataToken);

            case OperandType.InlineSwitch:
                var count = BitConverter.ToInt32(il, offset);
                offset += 4;
                var targets = new string[count];

                for (var index = 0; index < count; index++)
                {
                    var branchDelta = BitConverter.ToInt32(il, offset);
                    offset += 4;
                    targets[index] = $"IL_{offset + branchDelta:X4}";
                }

                return string.Join(", ", targets);

            default:
                return "<unsupported operand>";
        }
    }

    private static string ResolveMetadataToken(Module module, int token)
    {
        try
        {
            return module.ResolveMember(token)?.ToString() ?? $"token-0x{token:X8}";
        }
        catch
        {
            return $"token-0x{token:X8}";
        }
    }
}
