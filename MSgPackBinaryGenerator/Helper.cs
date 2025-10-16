using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace MSgPackBinaryGenerator
{
    public static class Helper
    {
        public static string Indent(int level, string txt)
        {
            if (string.IsNullOrEmpty(txt))
            {
                return txt;
            }

            string tabs = new string('\t', level);

            StringBuilder sb = new StringBuilder(txt);
            var lines = txt.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None);
            var indentedLines = lines.Select(t => tabs + t);
            return string.Join(Environment.NewLine, indentedLines);
        }

        public static bool GetEnumIsFlags(List<EnumMember> value)
        {
            bool result;

            if (value.TrueForAll(t => t.IsFlags))
                result = true;
            else if (value.TrueForAll(t => t.IsFlags == false))
                result = false;
            else
                throw new Exception($"$Enum IsFlags must be consistent. | Enum : {value[0].EnumTypeName}");

            // 유효한 Flag 값 세팅 여부 검사 (0,1,2,4,8 .. ) 또는 (1,2,4,8. ...)
            if (result)
            {
                int flagBit = value[0].Value;
                if (flagBit != 0x0 && flagBit != 0x1)
                {
                    throw new Exception($"Enum Flags value must be PowerOfTwo. | Enum : {value[0].EnumTypeName} | Member : {value[0].MemberName} | Value : {value[0].Value}");
                }

                for (int i = 0; i < value.Count; i++)
                {
                    if (value[i].Value != flagBit)
                    {
                        throw new Exception($"Enum Flags value must be PowerOfTwo | Enum : {value[0].EnumTypeName} | Member : {value[i].MemberName} | WrongValue : {value[i].Value}");
                    }

                    if (flagBit == 0x0)
                        flagBit = 1;
                    else
                        flagBit <<= 1;
                }
            }

            return result;
        }

        public static bool IsEnumByName(string name)
        {
            return name.StartsWith("E_");
        }

        public static DataRecordDataType ToDataType(string dataType)
        {
            if (string.IsNullOrEmpty(dataType))
            {
                throw new ArgumentException($"Wrong DataType");
            }

            if (dataType == "string")
            {
                return DataRecordDataType.String;
            }
            else if (IsEnumByName(dataType))
            {
                return DataRecordDataType.Enum;
            }

            return DataRecordDataType.Normal;
        }

        public static bool IsPowerOfTwo(int n)
        {
            // 1. n이 0보다 커야 함 (음수와 0은 2의 거듭제곱이 아님)
            // 2. 비트 연산: n & (n - 1) == 0 인지 확인
            //    n이 2의 거듭제곱일 때만 이 조건이 참이 됨
            return n > 0 && (n & (n - 1)) == 0;
        }

        //public static Assembly CompileSource(string sourceCode)
        //{
        //    var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);

        //    string assemblyName = Path.GetRandomFileName();

        //    // ================== ▼▼▼ 이 부분을 수정합니다 ▼▼▼ ==================

        //    // 현재 실행중인 어플리케이션이 참조하는 모든 어셈블리를 가져옵니다.
        //    // 이것이 가장 확실하고 안정적인 방법입니다.
        //    var references = AppDomain.CurrentDomain.GetAssemblies()
        //        .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location)) // 동적 어셈블리 및 위치 없는 어셈블리 제외
        //        .Select(a => MetadataReference.CreateFromFile(a.Location))
        //        .ToList();

        //    // 만약 MessagePack의 특정 버전이나 추가적인 DLL이 필요하다면 여기에 수동으로 추가할 수도 있습니다.
        //    // 예: references.Add(MetadataReference.CreateFromFile(typeof(MessagePack.MessagePackSerializer).Assembly.Location));

        //    // ================== ▲▲▲ 여기까지 수정합니다 ▲▲▲ ==================

        //    var compilation = CSharpCompilation.Create(
        //        assemblyName,
        //        new[] { syntaxTree },
        //        references,
        //        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        //    );

        //    using var ms = new MemoryStream();
        //    var result = compilation.Emit(ms);

        //    if (!result.Success)
        //    {
        //        Console.WriteLine("❌ Compilation failed!");
        //        foreach (var diag in result.Diagnostics)
        //        {
        //            // 에러 메시지를 더 잘보이게 수정
        //            Console.Error.WriteLine(diag.ToString());
        //        }

        //        throw new Exception("Compilation failed");
        //    }

        //    ms.Seek(0, SeekOrigin.Begin);
        //    return Assembly.Load(ms.ToArray());
        //}
    }
}
