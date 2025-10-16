using System;
using System.Collections.Generic;
using System.Text;

namespace MSgPackBinaryGenerator
{
    public class DBContainerGenerator
    {
        CodeStringBuilder _builder = new CodeStringBuilder(2048);

        public override string ToString()
        {
            return _builder.Current.ToString();
        }

        public string Generate(List<TableSchemaDefinition> schemaData, bool includeUnitySupport = false)
        {
            _builder.AppendLine("//*** Auto Generation Code ***");
            _builder.AppendLine();
            _builder.AppendLine("using System;");
            _builder.AppendLine("using System.Collections.Generic;");
            if (includeUnitySupport)
                _builder.AppendLine("using UnityEngine;");
            _builder.AppendLine("using MessagePack;");
            _builder.AppendLine();
            _builder.AppendLine("namespace GameDB");
            _builder.OpenBracket();
            {
                _builder.AppendLine("public class GameDBContainer");
                _builder.OpenBracket();
                {
                    // 필드 추가
                    //   e.g public Dictionary<uint, Ability_Table> Ability_Table_data = new Dictionary<uint, Ability_Table>();
                    for (int i = 0; i < schemaData.Count; i++)
                    {
                        _builder.AppendLine(schemaData[i].ToSourceCode(DataTableSourceCodeForm.DictionaryField));

                        if (i != schemaData.Count - 1)
                            _builder.AppendLine();
                    }

                }
                _builder.CloseBracket();

                _builder.AppendLine();

                // MessagePack 클래스들 정의

                for (int i = 0; i < schemaData.Count; i++)
                {
                    var schema = schemaData[i];
                    _builder.AppendLine(schema.ToSourceCode(DataTableSourceCodeForm.DeclarationWithDeserialize));
                }
            }
            _builder.CloseBracket();

            return _builder.Current.ToString();
        }
    }
}