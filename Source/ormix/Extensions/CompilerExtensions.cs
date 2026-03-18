using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Reflection;
using Microsoft.CodeAnalysis.Emit;
using System.Text;
using System.Collections.Concurrent;

namespace Ormix.Extensions
{
    public static class CodeCompiler
    {
        private static readonly MetadataReference[] references = GetMetadataReferences();

        private static readonly ConcurrentDictionary<string, Assembly> cachedAssemblies = new();

        private static MetadataReference[] GetMetadataReferences()
        {
            var assemblyPath = Path.GetDirectoryName(typeof(object).Assembly.Location);
            if (string.IsNullOrEmpty(assemblyPath))
                return new MetadataReference[0];

            return new MetadataReference[]
            {
               MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
               MetadataReference.CreateFromFile(Assembly.GetExecutingAssembly().Location),
               MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
               MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "mscorlib.dll")),
               MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.dll")),
               MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Private.CoreLib.dll")),
               MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Core.dll")),
               MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Runtime.dll")),
               MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Collections.dll")),
            };
        }

        public static Assembly Compile(string codeIdentifier, string code)
        {
            ArgumentNullException.ThrowIfNull(codeIdentifier, nameof(codeIdentifier));
            ArgumentNullException.ThrowIfNull(code, nameof(code));

            if (cachedAssemblies.TryGetValue(codeIdentifier, out Assembly? assembly))
                return assembly;


            CSharpCompilation cSharpCompilation;
            string assemblyName = Path.GetRandomFileName();
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(code);
            lock (references)
            {
                cSharpCompilation = CSharpCompilation.Create(
                    assemblyName: assemblyName,
                    syntaxTrees: new[] { syntaxTree },
                    references: references,
                    options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            }

            using var memoryStream = new MemoryStream();
            EmitResult emitResult = cSharpCompilation.Emit(memoryStream);
            if (!emitResult.Success)
            {
                IEnumerable<Diagnostic> failures = emitResult.Diagnostics
                    .Where(diagnostic => diagnostic.IsWarningAsError ||
                           diagnostic.Severity == DiagnosticSeverity.Error);

                var messages = new StringBuilder()
                    .AppendLine($"Generated class : {code}");
                foreach (Diagnostic diagnostic in failures)
                    messages.AppendLine(string.Format("Failed to compile code '{0}'! {1}: {2}",
                        codeIdentifier, diagnostic.Id, diagnostic.GetMessage()));

                if (failures.Any())
                    throw new CompilerException(messages.ToString());
            }

            memoryStream.Seek(0, SeekOrigin.Begin);
            var newAssembly = Assembly.Load(memoryStream.ToArray());
            cachedAssemblies[codeIdentifier] = newAssembly;
            return newAssembly;
        }
    }

    [Serializable]
    public class CompilerException : Exception
    {
        public CompilerException()
        {
        }

        public CompilerException(string message)
            : base(message)
        {
        }

        public CompilerException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }

}
