using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace MSgPackBinaryGenerator
{
    public static class RuntimeCompiler
    {
        // 공통 참조 로직
        private static List<MetadataReference> BuildDefaultReferences(string[] additionalReferences = null)
        {
            var refs = new List<MetadataReference>();

            var assemblies = new[]
            {
                typeof(object).Assembly,                     // System.Private.CoreLib
                typeof(Console).Assembly,                    // System.Console
                typeof(Enumerable).Assembly,                 // System.Linq
                typeof(List<>).Assembly,                     // System.Collections
                typeof(System.Runtime.AssemblyTargetedPatchBandAttribute).Assembly, // System.Runtime
                Assembly.Load("netstandard"),
                Assembly.Load("System.Private.CoreLib"),
                Assembly.Load("System.Runtime"),
                Assembly.Load("System.Collections"),
                Assembly.Load("System.Console"),
                Assembly.Load("System.Linq"),
                Assembly.Load("System.Memory"),
            };

            foreach (var asm in assemblies.Distinct())
            {
                if (string.IsNullOrEmpty(asm.Location))
                {
                    Console.WriteLine($"(Critical) AssemblyLocation({asm}) is Empty");
                }
                refs.Add(MetadataReference.CreateFromFile(asm.Location));
            }

            // MessagePack 관련 강제 추가
            try
            {
                var mpAsm = Assembly.Load("MessagePack");
                var mpAnno = Assembly.Load("MessagePack.Annotations");
                refs.Add(MetadataReference.CreateFromFile(mpAsm.Location));
                refs.Add(MetadataReference.CreateFromFile(mpAnno.Location));

                // Resolver 및 Serializer 타입 포함
                refs.Add(MetadataReference.CreateFromFile(typeof(MessagePack.Resolvers.StandardResolver).Assembly.Location));
                refs.Add(MetadataReference.CreateFromFile(typeof(MessagePack.MessagePackSerializer).Assembly.Location));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ MessagePack assembly reference failed: {ex.Message}");
            }

            // 추가 참조가 있다면
            if (additionalReferences != null)
            {
                foreach (var path in additionalReferences)
                {
                    if (File.Exists(path))
                        refs.Add(MetadataReference.CreateFromFile(path));
                }
            }

            return refs;
        }

        // 메모리 컴파일
        public static Assembly CompileSource(string sourceCode, string[] additionalReferences = null)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);

            var compilation = CSharpCompilation.Create(
                $"RuntimeAssembly_{Guid.NewGuid():N}",
                new[] { syntaxTree },
                BuildDefaultReferences(additionalReferences),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );

            using var ms = new MemoryStream();
            var emitResult = compilation.Emit(ms);

            if (!emitResult.Success)
            {
                var errors = string.Join("\n", emitResult.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => d.ToString()));

                Console.WriteLine($"*** Compilation ERROR ! : {errors}");
                return null;
            }

            ms.Seek(0, SeekOrigin.Begin);
            return Assembly.Load(ms.ToArray());
        }
    }
}
