using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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

        public static bool IsArray(string typeName)
        {
            return typeName.NoSpace().EndsWith("[]");
        }

        public static DataRecordDataType ToDataType(string dataType)
        {
            if (string.IsNullOrEmpty(dataType))
            {
                throw new ArgumentException($"Wrong DataType");
            }

            dataType = dataType.NoSpace().Replace("[]", "");

            if (dataType.Equals("int", StringComparison.OrdinalIgnoreCase))
            {
                return DataRecordDataType.Integer;
            }
            if (dataType.Equals("uint", StringComparison.OrdinalIgnoreCase))
            {
                return DataRecordDataType.Uint;
            }
            else if (dataType.Equals("long", StringComparison.OrdinalIgnoreCase))
            {
                return DataRecordDataType.Long;
            }
            else if (dataType.Equals("ulong", StringComparison.OrdinalIgnoreCase))
            {
                return DataRecordDataType.Ulong;
            }
            else if (dataType.Equals("bool", StringComparison.OrdinalIgnoreCase) || dataType.Equals("boolean", StringComparison.OrdinalIgnoreCase))
            {
                return DataRecordDataType.Boolean;
            }
            else if (dataType.Equals("float", StringComparison.OrdinalIgnoreCase))
            {
                return DataRecordDataType.Float;
            }
            else if (dataType.Equals("double", StringComparison.OrdinalIgnoreCase))
            {
                return DataRecordDataType.Double;
            }
            else if (dataType.Equals("string", StringComparison.OrdinalIgnoreCase))
            {
                return DataRecordDataType.String;
            }
            else if (IsEnumByName(dataType))
            {
                return DataRecordDataType.Enum;
            }
            else if (dataType.Equals("Vector2Int", StringComparison.OrdinalIgnoreCase))
            {
                return DataRecordDataType.Vector2Int;
            }

            Console.WriteLine($"** 데이터 타입이 기존에 정의한 데이터 타입을 벗어난다 !! : {dataType}");
            return DataRecordDataType.Etc;
        }

        public static string ValueStringToCode(string rawTypeName, string value, Func<string, bool> isEnumFlagChecker)
        {
            rawTypeName = rawTypeName.NoSpace();
            string valueSuffix = string.Empty;
            bool isArray = IsArray(rawTypeName);
            var dataType = ToDataType(rawTypeName);

            // Array 여부 체크 
            // e.g value => (1,2,3) ? 1,2,3 ? [1,2,3] ?  다 지원해줄까? 
            if (isArray)
            {
                // e.g new int[] { 
                valueSuffix = $"new {rawTypeName} {{ ";
            }

            if (dataType == DataRecordDataType.Integer ||
                dataType == DataRecordDataType.Uint ||
                dataType == DataRecordDataType.Long ||
                dataType == DataRecordDataType.Ulong)
            {
                if (isArray)
                {
                    // 1,2,3,4 .. 형태로 
                    value = value.GetElementsFromArrayInSingleLine(false);
                }
                else
                {
                    // 배열아니면 그냥 패스
                    value = $"{value}";
                }
            }
            else if (dataType == DataRecordDataType.Boolean)
            {
                if (isArray)
                {
                    value = string.Join(',',
                        value.GetElementsFromArrayInSingleLine(false).Split(',').
                        Select(t => $"bool.Parse(\"{t}\")"));
                }
                else
                {
                    // bool 로 파싱 e.g bool.Parse("true")
                    value = $"bool.Parse(\"{value}\")";
                }
            }
            else if (dataType == DataRecordDataType.Float)
            {
                if (isArray)
                {
                    value = string.Join(',',
                        value.GetElementsFromArrayInSingleLine(false).Split(',').
                        Select(t => $"float.Parse(\"{t}\")"));
                }
                else
                {
                    // 뒤에 f추가 (e.g 0.5f)
                    value = $"{value}f";
                }
            }
            else if (dataType == DataRecordDataType.Double)
            {
                if (isArray)
                {
                    value = string.Join(',',
                        value.GetElementsFromArrayInSingleLine(false).Split(',').
                        Select(t => $"double.Parse(\"{t}\")"));
                }
                else
                {
                    // 문자 그대로 넣어주면 됨 (e.g 0.5)
                    value = $"{value}";
                }
            }
            else if (dataType == DataRecordDataType.Vector2Int)
            {
                // value 가 1,2,3 이나 1.3,1.5 이런식이나 
                // value 에 (1,2) 이런식으로 들어옴 
                if (isArray)
                {
                    // [0] : "1,2"
                    // [1] : "3,4" ...
                    var elements = value.ExtractDataElementsFromArray(true);
                    value = string.Join(',', elements.Select(e =>
                    {
                        var pos = e.Split(',');
                        return $"new Vector2Int({pos[0]},{pos[1]})";
                    }));
                }
                else
                {
                    // 문자 그대로 넣어주면 됨 (e.g 0.5)
                    value = $"new Vector2Int({value.StripAllBrackets()})";
                }
            }
            else if (dataType == DataRecordDataType.Enum)
            {
                // **주의 **
                // 배열은 rawTypeName 에 "Enum_타입_이름[]" 이런식으로 전달중이니
                // 멤버 이름 앞에 추가해줘야 하는 Enum 의 특성상
                // 이 부분을 제거 처리한 부분을 별도로 보관.
                var enumTypeName = rawTypeName.StripAllBrackets();

                // Enum 타입일때 플래그일때와 일반 Enum 타입에 따라 다르게 처리 필요
                if (isEnumFlagChecker(enumTypeName))
                {
                    if (isArray)
                    {
                        // 가독성 너무 거지같아서 조금이라도 나눔 ..
                        // 1. value : "Member01|Member02|Member03,Member01|Member03"
                        //  [0] : "Member01|Member02|Member03"
                        //  [1] : "Member01|Member03"
                        var step01 = value.GetElementsFromArrayInSingleLine(false).Split(',');
                        // 2. "E_Enum.Member01|E_Enum.Member02|E_Enum.Member03,E_Enum.Member01|E_Enum.Member03" 으로 조합 
                        value = string.Join(',', step01.Select(e => string.Join('|', e.Split('|').Select(t02 => $"{enumTypeName}.{t02}"))));
                    }
                    else
                    {
                        string combined = string.Join('|', value.Split('|').Select(memberName => $"{enumTypeName}.{memberName}"));
                        value = combined;
                    }
                }
                else
                {
                    if (isArray)
                    {
                        value = string.Join(',',
                            value.GetElementsFromArrayInSingleLine(false).Split(',').
                            Select(e => $"{enumTypeName}.{e}"));
                    }
                    else
                    {
                        value = $"{enumTypeName}.{value}";
                    }
                }
            }
            else if (dataType == DataRecordDataType.String)
            {
                if (isArray)
                {
                    value = string.Join(',',
                        value.GetElementsFromArrayInSingleLine(false).Split(',').
                        Select(e => $"\"{e}\""));
                }
                else
                {
                    value = $"\"{value}\"";
                }
            }

            return value;
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

    public static class StringEx
    {
        public static string NoSpace(this string str)
        {
            return str.Replace(" ", "");
        }

        public static string StripQuotes(this string str)
        {
            return str.NoSpace().Replace("\'", "").Replace("\"", "");
        }

        public static string StripAllBrackets(this string str)
        {
            return str.NoSpace().Replace("(", "").Replace(")", "").Replace("[", "").Replace("]", "");
        }

        public static string GetElementsFromArrayInSingleLine(this string str, bool multipleValuesPerElements)
        {
            /*
             * e.g 
             * 다음 케이스에 대해 요소들만 Comma(,) 로 구분짓는 기존 상태로 반환해줌 
             *  1,2,3,4 => [0] : 1 , [1] : 2 , [2] : 3 , [3] : 4
             *  (1,2,3,4) => 위와 동일
             *  [1,2,3,4] => 위와 동일
             *  (1,2),(3,4) => [0] : 1,2 , [1] : 3,4
             *  [1,2],[3,4] => 위와동일
             * */
            if (multipleValuesPerElements)
            {
                // 괄호/대괄호 둘다 지원하기 위해 처리
                if (str.Contains("("))
                {
                    // (1,2),(3,4) => 1,2),(3,4 => Split("),(")
                    return str.NoSpace().Substring(1, str.Length - 2);
                }
                else if (str.Contains("["))
                {
                    // [1,2],[3,4] => 1,2],[3,4 => Split("],[")
                    return str.NoSpace().Substring(1, str.Length - 2);
                }
            }
            else
            {
                return str.NoSpace().StripAllBrackets();
            }

            return null;
        }

        public static string[] ExtractDataElementsFromArray(this string str, bool multipleValuesPerElements)
        {
            if (multipleValuesPerElements)
            {
                // 괄호/대괄호 둘다 지원하기 위해 처리
                if (str.Contains("("))
                {
                    // (1,2),(3,4) => 1,2),(3,4 => Split("),(")
                    return str.GetElementsFromArrayInSingleLine(multipleValuesPerElements).Split("),(");
                }
                else if (str.Contains("["))
                {
                    // [1,2],[3,4] => 1,2],[3,4 => Split("],[")
                    return str.GetElementsFromArrayInSingleLine(multipleValuesPerElements).Split("],[");
                }
            }
            else
            {
                return str.GetElementsFromArrayInSingleLine(multipleValuesPerElements).Split(',');
            }

            return null;
        }
    }
}
