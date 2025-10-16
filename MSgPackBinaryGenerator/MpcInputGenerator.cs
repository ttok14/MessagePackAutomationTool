using System;
using System.Collections.Generic;
using System.Text;

namespace MSgPackBinaryGenerator
{
    public class MpcInputGenerator
    {
        CodeStringBuilder _builder = new CodeStringBuilder(2048);

        public override string ToString()
        {
            return _builder.Current.ToString();
        }

        public string Generate(TableContainer[] tableContainer, EnumGroups enumGroup)
        {
            _builder.AppendLine("using System;");
            _builder.AppendLine("using System.Collections.Generic;");
            _builder.AppendLine("using MessagePack;");
            _builder.AppendLine();
            _builder.AppendLine("namespace GameDB");
            _builder.OpenBracket();
            {
                _builder.AppendLine(enumGroup.ToSourceCode(EnumGroupsSourceCodeForm.Groups));

                // 필드 추가
                //   e.g public Dictionary<uint, Ability_Table> Ability_Table_data = new Dictionary<uint, Ability_Table>();

                foreach (var container in tableContainer)
                {
                    _builder.AppendLine(container.SchemaData.ToSourceCode(DataTableSourceCodeForm.DeclarationOnlyFields));
                    _builder.AppendLine();
                }
                //for (int i = 0; i < schemaData.Count; i++)
                //{
                //    _builder.AppendLine(schemaData[i].ToSourceCode(DataTableSourceCodeForm.DictionaryField));
                //    _builder.AppendLine();
                //}

                _builder.AppendLine();

                // MessagePack 클래스들 정의

                //for (int i = 0; i < schemaData.Count; i++)
                //{
                //    var schema = schemaData[i];
                //    _builder.AppendLine(schema.ToSourceCode(DataTableSourceCodeForm.DeclarationWithDeserialize));
                //}
            }
            _builder.CloseBracket();

            return _builder.Current.ToString();
        }
    }
}