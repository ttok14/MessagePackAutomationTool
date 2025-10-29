using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;

namespace MSgPackBinaryGenerator
{
    #region ===:: 데이터 클래스 스키마 관련 ::===

    // :: 하나의 컬럼 스키마 데이터
    //  !MessagePack 을 위한 하나의 완전한 클래스 데이터 타입의 하나의 필드를 정의!
    public class TableSchemaDefinitionField
    {
        public string Name { get; set; }
        public string TypeName { get; set; }
        public int KeyIndex { get; set; }

        public bool IsArray => Helper.IsArray(TypeName);
        // 이 규칙을 잘 준수해야함. (enum 은 E_ 로 시작한다)
        public DataRecordDataType Type => Helper.ToDataType(TypeName);
    }

    // 데이터 테이블 하나의 모든 컬럼 스키마 데이터
    // !하나의 완전한 MessagePack 용 클래스 타입 하나를 정의!
    public class TableSchemaDefinition : IToSourceCode<DataTableSourceCodeForm>
    {
        public string TableName { get; set; }
        // 해당 클래스 타입을 이루는 각종 필드에 대한 정보
        public List<TableSchemaDefinitionField> Fields { get; set; }

        public string IDName => Fields[0].Name;
        public string IDType => Fields[0].TypeName;

        /// <summary>
        /// 조립 생성자 
        /// </summary>
        /// <param name="tableName">테이블 명</param>
        /// <param name="columns">해당 테이블의 컬럼들 (Comma으로 구분지어진 csv형태)</param>
        /// <param name="types"></param>
        public TableSchemaDefinition(string tableName, string columns, string types)
        {
            TableName = tableName;
            var columnNames = columns.Split(',');
            var typeNames = types.Split(',');

            if (columnNames.Length != typeNames.Length)
            {
                StringBuilder sb = new StringBuilder();
                throw new ArgumentException($"Failed | Columns And Types Length does not match. \nTableName : {tableName} | Columns: {columns} | Types: {types}");
            }

            Fields = new List<TableSchemaDefinitionField>();

            for (int i = 0; i < columnNames.Length; i++)
            {
                Fields.Add(new TableSchemaDefinitionField()
                {
                    Name = columnNames[i],
                    TypeName = typeNames[i],
                    KeyIndex = i
                });
            }
        }

        public string ToSourceCode(DataTableSourceCodeForm type)
        {
            switch (type)
            {
                case DataTableSourceCodeForm.DeclarationOnlyFields:
                    {
                        CodeStringBuilder sb = new CodeStringBuilder(512);

                        sb.AppendLine("[MessagePackObject]");
                        sb.AppendLine($"public class {TableName}");
                        sb.OpenBracket();
                        {
                            for (int i = 0; i < Fields.Count; i++)
                            {
                                var field = Fields[i];
                                sb.AppendLine($"[Key({i})]");
                                sb.AppendLine($"public {field.TypeName} {field.Name};");
                            }
                        }
                        sb.CloseBracket();

                        return sb.ToString();
                    }
                case DataTableSourceCodeForm.DeclarationWithDeserialize:
                    {
                        CodeStringBuilder toInsert = new CodeStringBuilder(512, 1);

                        toInsert.AppendLine();
                        toInsert.AppendLine("[UnityEngine.Scripting.Preserve]");
                        toInsert.AppendLine($"public static Dictionary<{IDType}, {TableName}> Deserialize(ref byte[] _readBytes)");
                        toInsert.OpenBracket();
                        {
                            toInsert.AppendLine($"Dictionary<{IDType}, {TableName}> dicTables = new Dictionary<{IDType}, {TableName}>();");
                            toInsert.AppendLine($"MessagePackReader reader = new MessagePackReader(new System.ReadOnlyMemory<byte>(_readBytes));");
                            toInsert.AppendLine("int tableCount = MessagePackSerializer.Deserialize<int>(ref reader);");
                            toInsert.AppendLine("for (int i = 0; i < tableCount; i++)");
                            toInsert.OpenBracket();
                            {
                                toInsert.AppendLine($"var table = MessagePackSerializer.Deserialize<{TableName}>(ref reader);");

                                toInsert.AppendLine($"dicTables.Add(table.{IDName}, table);");
                            }
                            toInsert.CloseBracket();

                            toInsert.AppendLine("return dicTables;");
                        }
                        toInsert.CloseBracket();

                        CodeStringBuilder insertInto = new CodeStringBuilder(ToSourceCode(DataTableSourceCodeForm.DeclarationOnlyFields));

                        return insertInto.InsertAtCloseBracket(toInsert.ToString(), 0, true).ToString();
                    }
                case DataTableSourceCodeForm.DictionaryField:
                    {
                        return $"public Dictionary<{IDType}, {TableName}> {TableName}_data = new Dictionary<{IDType}, {TableName}>();";
                    }
                default:
                    throw new NotImplementedException($"Not implented option : {type}");
            }
        }
    }

    #endregion

    #region ===:: 실제 테이블별 데이터 홀더 ::===

    // 하나의 행 데이터를 이루는 하나의 최소 단위 데이터
    public class TableValueRecord
    {
        public string Value { get; set; }
        public TableSchemaDefinitionField SchemaData;
    }

    // 하나의 행 데이터
    public class TableValueElement
    {
        public List<TableValueRecord> Records = new List<TableValueRecord>();
    }

    // 하나의 테이블의 모든 행 데이터를 가지고 있는 그룹 데이터
    public class TableDataDefinition
    {
        public List<TableValueElement> DataList;

        public TableDataDefinition(string dataCsvPath, string[] columns, TableSchemaDefinition schemaData)
        {
            var schemaLookupTable = new Dictionary<string, TableSchemaDefinitionField>();
            DataList = new List<TableValueElement>();

            // CsvHelper 구성
            var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                TrimOptions = TrimOptions.Trim
            };

            using (var reader = new StreamReader(dataCsvPath))
            using (var csv = new CsvHelper.CsvReader(reader, config))
            {
                csv.Read();
                csv.ReadHeader();

                while (csv.Read())
                {
                    var newElement = new TableValueElement();
                    for (int j = 0; j < columns.Length; j++)
                    {
                        var column = columns[j];
                        string value = csv.GetField(j).StripQuotes();

                        if (schemaLookupTable.TryGetValue(column, out var schema) == false)
                        {
                            schema = schemaData.Fields.Find(t => t.Name == column);
                            if (schema == null)
                            {
                                throw new Exception($"Failed to retrieve Schema data due to Non matching Schema Name : {column}");
                            }
                        }

                        newElement.Records.Add(new TableValueRecord()
                        {
                            SchemaData = schema,
                            Value = value
                        });
                    }
                    DataList.Add(newElement);
                }
            }
        }
    }

    #endregion

    #region ===:: 스키마 + 데이터 통합 ::===

    public class TableContainer
    {
        public TableSchemaDefinition SchemaData { get; private set; }
        public TableDataDefinition TableData { get; private set; }

        public TableContainer(TableSchemaDefinition schemaData, TableDataDefinition tableData)
        {
            SchemaData = schemaData;
            TableData = tableData;
        }
    }

    #endregion

    #region ===:: Enum 관련 ::===

    // 하나의 Enum 요소의 멤버를 정의
    public class EnumMember
    {
        // !! 테이블과 매칭돼야함 (CSVReader 로 읽음) !! //

        public string EnumTypeName { get; set; }
        public bool IsFlags { get; set; }
        public string MemberName { get; set; }
        public int Value { get; set; }
        public string Description { get; set; }

        public override string ToString()
        {
            return $"EnumTypeName : {EnumTypeName}, IsFlags : {IsFlags}, MemberName: {MemberName}, Value: {Value}, Description: {Description}";
        }
    }

    public class EnumDefinition : IToSourceCode<EnumDefinitionSourceCodeForm>
    {
        public string EnumName;
        public List<EnumMember> Members = new List<EnumMember>();
        public bool IsFlags { get; set; }

        public string ToSourceCode(EnumDefinitionSourceCodeForm type)
        {
            switch (type)
            {
                case EnumDefinitionSourceCodeForm.Declaration:
                    {
                        CodeStringBuilder sb = new CodeStringBuilder(256);

                        if (IsFlags)
                            sb.AppendLine("[System.Flags]");

                        sb.AppendLine($"public enum {EnumName}");
                        sb.OpenBracket();
                        {
                            foreach (var m in Members)
                            {
                                sb.AppendLine($"{m.MemberName} = {m.Value}, /* {m.Description} */");
                            }
                        }
                        sb.CloseBracket();

                        return sb.ToString();
                    }
                default:
                    throw new NotImplementedException($"Not implented option : {type}");
            }
        }
    }

    // 하나의 완전한 Enum 을 정의
    public class EnumGroups : IToSourceCode<EnumGroupsSourceCodeForm>
    {
        public Dictionary<string, EnumDefinition> Enums;

        public EnumGroups(string columns, string[] raws)
        {
            Enums = new Dictionary<string, EnumDefinition>();

            string csvData = string.Join("\n", raws);
            var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = false,
                TrimOptions = TrimOptions.Trim
            };

            var columnNames = columns.Split(',');

            using (var reader = new StringReader(csvData))
            using (var csv = new CsvReader(reader, csvConfig))
            {
                var records = csv.GetRecords<EnumMember>().ToList();
                foreach (var enumMember in records)
                {
                    if (Helper.IsEnumByName(enumMember.EnumTypeName) == false)
                    {
                        throw new ArgumentException($"Enum Type Must Start with \'E_\' | WrongName : {enumMember.EnumTypeName}");
                    }

                    if (Enums.TryGetValue(enumMember.EnumTypeName, out var schema) == false)
                    {
                        schema = new EnumDefinition()
                        {
                            EnumName = enumMember.EnumTypeName,
                            IsFlags = enumMember.IsFlags,
                        };

                        Enums.Add(enumMember.EnumTypeName, schema);
                    }

                    schema.Members.Add(enumMember);
                }
            }

            foreach (var e in Enums)
            {
                e.Value.IsFlags = Helper.GetEnumIsFlags(e.Value.Members);
            }
        }

        public string ToSourceCode(EnumGroupsSourceCodeForm type)
        {
            switch (type)
            {
                case EnumGroupsSourceCodeForm.Groups:
                    {
                        CodeStringBuilder sb = new CodeStringBuilder(512);

                        foreach (var e in Enums)
                        {
                            sb.AppendLine(e.Value.ToSourceCode(EnumDefinitionSourceCodeForm.Declaration));
                        }

                        return sb.ToString();
                    }
                default:
                    throw new NotImplementedException($"Not implented option : {type}");
            }
        }
    }

    #endregion

    #region ====:: Metadata 정의 ::====

    public class FileMetadata
    {
        public string Name { get; set; }
        public string Hash { get; set; }
        public long ByteSize { get; set; }
    }

    public class MasterMetadata
    {
        public string Version { get; set; }
        public string TotalHash { get; set; }
        public List<FileMetadata> Files { get; set; } = new List<FileMetadata>();
    }

    #endregion
}
