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

        // 일반적인 숫자 등 
        Normal,
        // 불리언
        Boolean,
        // 문자열 (" ")
        String,
        // Enum 타입 ( E_xxx.xxx )
        Enum
    }
}
