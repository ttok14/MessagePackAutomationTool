using System;
using System.Collections.Generic;
using System.Text;

namespace MSgPackBinaryGenerator
{
    public class CodeStringBuilder
    {
        public StringBuilder Current { get; private set; }
        public int IndentLevel = 0;

        public CodeStringBuilder(int capacity = 0)
        {
            Current = new StringBuilder(capacity);
        }

        public void Append(string str)
        {
            Current.Append(str);
        }

        public void AppendLine(string str = "")
        {
            Current.AppendLine(Indented(str));
        }

        public void OpenBracket()
        {
            AppendLine("{");
            IndentLevel++;
        }

        public void CloseBracket()
        {
            IndentLevel--;
            AppendLine("}");
        }

        string Indented(string str)
        {
            if (string.IsNullOrEmpty(str))
            {
                return str;
            }

            string prefix = string.Empty;

            for (int i = 0; i < IndentLevel; i++)
            {
                prefix += "\t";
            }

            return prefix + str;
        }
    }
}
