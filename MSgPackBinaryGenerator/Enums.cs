using System;
using System.Collections.Generic;
using System.Text;

namespace MSgPackBinaryGenerator
{
    public enum DataTableSourceCodeForm
    {
        None = -1,

        DeclarationOnlyFields,
        DeclarationWithDeserialize,
        DictionaryField
    }

    public enum EnumDefinitionSourceCodeForm
    {
        None = -1,
        Declaration
    }

    public enum EnumGroupsSourceCodeForm
    {
        None = -1,
        Groups
    }

    public enum DataRecordDataType
    {
        None = -1,

        // 정수
        Integer,
        Uint,
        Long,
        Ulong,
        // 소수 Single
        Float,
        // 소수
        Double,
        // 유니티 Vector2Int 타입
        Vector2Int,
        // 유니티 Vector3 타입
        Vector3,
        // 불리언
        Boolean,
        // 문자열 (" ")
        String,
        // Enum 타입 ( E_xxx.xxx )
        Enum,

        Etc
    }
}
