using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
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

            if (dataType.Equals("bool", StringComparison.OrdinalIgnoreCase) || dataType.Equals("boolean", StringComparison.OrdinalIgnoreCase))
            {
                return DataRecordDataType.Boolean;
            }
            else if (dataType.Equals("string", StringComparison.OrdinalIgnoreCase))
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

        public static string ExtractCommandArgument(string[] args, string targetArg)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == targetArg && i < args.Length - 1)
                {
                    return args[i + 1];
                }
            }
            return string.Empty;
        }

        public static string GetHashByFilePath(string filePath)
        {
            using (var stream = File.OpenRead(filePath))
            using (var sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(stream);
                string hashString = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

                return hashString;
            }
        }

        public static string GetHashByString(string str)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(str));
                string hashString = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

                return hashString;
            }
        }
    }
}
