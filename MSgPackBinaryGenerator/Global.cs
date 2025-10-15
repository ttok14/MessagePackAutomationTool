using System;
using System.Collections.Generic;
using System.Text;

namespace MSgPackBinaryGenerator
{
    public static class Global
    {
        public static Dictionary<string, List<TableSchemaDefinition>> TableSchemaByTableName { get; set; }
        public static Dictionary<string, List<EnumGroups>> EnumSchemaByEnumName { get; set; }
        public static string GameDBContainerSourceCode { get; set; }
        public static string EnumDBSourceCode { get; set; }
    }
}
