using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace MSgPackBinaryGenerator
{
    public static class RuntimeCompiler
    {
        // AI 통해서 코드 생성
        public static Assembly CompileSource(string sourceCode)
        {
            // 기존의 메모리 기반 컴파일 함수
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
            var references = BuildDefaultReferences();

            var compilation = CSharpCompilation.Create(
                assemblyName: Path.GetRandomFileName(),
                syntaxTrees: new[] { syntaxTree },
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );

            using var ms = new MemoryStream();
            var emitResult = compilation.Emit(ms);
            if (!emitResult.Success)
            {
                var errors = string.Join("\n", emitResult.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => d.ToString()));
                throw new Exception($"❌ Compilation failed!\n{errors}");
            }

            ms.Seek(0, SeekOrigin.Begin);
            return Assembly.Load(ms.ToArray());
        }

        // ✅ 새로 추가되는 함수
        public static void CompileSourceToDll(string sourceCode, string outputPath)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
            var references = BuildDefaultReferences();

            var compilation = CSharpCompilation.Create(
                assemblyName: Path.GetFileNameWithoutExtension(outputPath),
                syntaxTrees: new[] { syntaxTree },
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );

            // 출력 디렉터리 확인 및 생성
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

            using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
            var emitResult = compilation.Emit(fs);

            if (!emitResult.Success)
            {
                var errors = string.Join("\n", emitResult.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => d.ToString()));
                throw new Exception($"❌ DLL Compilation failed!\n{errors}");
            }

            Console.WriteLine($"✅ DLL compiled successfully: {outputPath}");
        }

        // 공통 참조 등록 함수 (AI 통해서 코드 생성)
        private static List<MetadataReference> BuildDefaultReferences()
        {
            var refs = new List<MetadataReference>();

            // 기본 어셈블리들
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
                refs.Add(MetadataReference.CreateFromFile(asm.Location));
            }

            // MessagePack 관련 추가
            try
            {
                var mpAsm = Assembly.Load("MessagePack");
                var mpAnno = Assembly.Load("MessagePack.Annotations");
                refs.Add(MetadataReference.CreateFromFile(mpAsm.Location));
                refs.Add(MetadataReference.CreateFromFile(mpAnno.Location));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ MessagePack assembly reference failed: {ex.Message}");
            }

            return refs;
        }
    }
}
