using System;
using System.Collections.Generic;

namespace MSgPackBinaryGenerator
{
    // :: 하나의 컬럼 스키마 데이터
    //  !MessagePack 을 위한 하나의 완전한 클래스 데이터 타입의 하나의 필드를 정의!
    public class TableSchemaField
    {
        public string Name { get; set; }
        public string TypeName { get; set; }
        public int KeyIndex { get; set; }

        public bool IsEnum => TypeName.StartsWith("e");
    }

    // 데이터 테이블 하나의 모든 컬럼 스키마 데이터
    // !하나의 완전한 MessagePack 용 클래스 타입 하나를 정의!
    public class TableSchemaContents
    {
        public string TableName { get; set; }
        // 해당 클래스 타입을 이루는 각종 필드에 대한 정보
        public List<TableSchemaField> Fields { get; set; }

        /// <summary>
        /// 조립 생성자 
        /// </summary>
        /// <param name="tableName">테이블 명</param>
        /// <param name="columnsWithComma">해당 테이블의 컬럼들 (Comma으로 구분지어진 csv형태)</param>
        /// <param name="typesWithComma"></param>
        public TableSchemaContents(string tableName, string columnsWithComma, string typesWithComma)
        {
            TableName = tableName;
            var columnNames = columnsWithComma.Split(',');
            var typeNames = typesWithComma.Split(',');

            if (columnNames.Length != typeNames.Length)
            {
                throw new ArgumentException($"Failed | Columns And Types Length does not match. \nColumns: {columnNames.Length} Types: {typeNames.Length}");
            }

            Fields = new List<TableSchemaField>();

            for (int i = 0; i < columnNames.Length; i++)
            {
                Fields.Add(new TableSchemaField()
                {
                    Name = columnNames[i],
                    TypeName = typeNames[i],
                    KeyIndex = i
                });
            }
        }

        public string IDName => Fields[0].Name;
        public string IDType => Fields[0].TypeName;
    }

    // 하나의 Enum 요소의 멤버를 정의
    public class EnumMemberSchema
    {
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

    // 하나의 완전한 Enum 을 정의
    public class EnumSchemaContents
    {
        public Dictionary<string, List<EnumMemberSchema>> Enums;
        // public List<EnumSchemaField> Fields { get; set; }

        public EnumSchemaContents(string columns, string[] raws)
        {
            var columnNames = columns.Split(',');
            //if (columnNames.Length != raws[0].Split(',').Length)
            //{
            //    throw new ArgumentException($"Failed | Columns And Raws Length does not match. \nColumns: {columnNames.Length} Raws: {raws.Length}");
            //}

            // Fields = new List<EnumSchemaField>();
            Enums = new Dictionary<string, List<EnumMemberSchema>>();

            for (int i = 0; i < raws.Length; i++)
            {
                var rawNames = raws[i].Split(',');
                if (columnNames.Length != rawNames.Length)
                {
                    throw new ArgumentException($"Failed | Columns And Raws Length does not match. \nColumns: {columnNames.Length} Raws: {rawNames.Length}");
                }

                string enumName = rawNames[0];
                if (Enums.ContainsKey(enumName) == false)
                {
                    Enums.Add(enumName, new List<EnumMemberSchema>(columnNames.Length));
                }

                Enums[enumName].Add(new EnumMemberSchema()
                {
                    EnumTypeName = rawNames[0],
                    IsFlags = bool.Parse(rawNames[1]),
                    MemberName = rawNames[2],
                    Value = int.Parse(rawNames[3]),
                    Description = rawNames[4],
                });
            }
        }
    }

    class DataTable_DataSheetContents
    {
        /*
         * e.g 
         * "ID,Name,ItemType,Price"
         * */
        public string column;
        /*
         * e.g 
         * [0] => "1,용사의검,Weapon,15000"
         * [1] => "2,전사의검,Weapon,23000"
         */
        public string[] raws;
    }

    class DataTable_SchemaSheetContents
    {
        /*
         * e.g.
         * "ID,Name,ItemType,Price"
         * */
        public string column;
        /*
         * e.g
         * "uint,string,eItemType,uint"
         */
        public string raw;
    }
}
