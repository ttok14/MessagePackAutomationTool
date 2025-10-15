
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

        public string Generate(EnumGroups enums)
        {
            _builder.AppendLine("//*** Auto Generation Code ***");
            _builder.AppendLine();
            _builder.AppendLine("namespace GameDB");
            _builder.OpenBracket();
            {
                _builder.AppendLine(enums.ToSourceCode(EnumGroupsSourceCodeForm.Groups));
            }
            _builder.CloseBracket();

            return _builder.Current.ToString();
        }

        //void BuildEnum(string enumName, bool isFlags, List<EnumMember> members)
        //{
        //    if (isFlags)
        //        _builder.AppendLine("[System.Flags]");

        //    _builder.AppendLine($"public enum {enumName}");
        //    _builder.OpenBracket();
        //    {
        //        for (int i = 0; i < members.Count; i++)
        //        {
        //            var m = members[i];
        //            _builder.AppendLine($"{m.MemberName} = {m.Value}, /* {m.Description} */");
        //        }
        //    }
        //    _builder.CloseBracket();
        //}
    }
}