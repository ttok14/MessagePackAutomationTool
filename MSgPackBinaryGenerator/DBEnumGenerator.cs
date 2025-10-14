using System;
using System.Collections.Generic;
using System.Text;

namespace MSgPackBinaryGenerator
{
    public class DBEnumContainer
    {
        CodeStringBuilder _builder = new CodeStringBuilder(2048);

        public override string ToString()
        {
            return _builder.Current.ToString();
        }

        public string Generate(EnumSchemaContents enums)
        {
            _builder.AppendLine("//*** Auto Generation Code ***");
            _builder.AppendLine();
            _builder.AppendLine("namespace GameDB");
            _builder.OpenBracket();
            {
                foreach (var e in enums.Enums)
                {
                    BuildEnum(e.Key, e.Value);
                }
            }
            _builder.CloseBracket();

            return _builder.Current.ToString();
        }

        void BuildEnum(string enumName, List<EnumMemberSchema> members)
        {
            _builder.AppendLine($"public enum {enumName}");
            _builder.OpenBracket();
            {
                for (int i = 0; i < members.Count; i++)
                {
                    var m = members[i];
                    _builder.AppendLine($"{m.MemberName} = {m.Value}, /* {m.Description} */");
                }
            }
            _builder.CloseBracket();
        }
    }
}