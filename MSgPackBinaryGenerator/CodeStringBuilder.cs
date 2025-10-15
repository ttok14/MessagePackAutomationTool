using System;
using System.Collections.Generic;
using System.Text;

namespace MSgPackBinaryGenerator
{
    public class CodeStringBuilder
    {
        public StringBuilder Current { get; private set; }
        public int IndentLevel = 0;

        public CodeStringBuilder(int capacity = 0, int indent = 0)
        {
            Current = new StringBuilder(capacity);
            IndentLevel = indent;
        }

        public CodeStringBuilder(string text, int baseIndent = 0)
        {
            Current = new StringBuilder(Helper.Indent(baseIndent, text));
            IndentLevel = baseIndent;
        }

        public override string ToString()
        {
            return Current.ToString();
        }

        public void Append(string str)
        {
            Current.Append(str);
        }

        public void AppendIndent(string str)
        {
            Current.Append(Helper.Indent(IndentLevel, str));
        }

        public CodeStringBuilder AppendLine(string str = "")
        {
            var stringsByLine = str.Split('\n');
            for (int i = 0; i < stringsByLine.Length; i++)
            {
                Current.AppendLine(Helper.Indent(IndentLevel, stringsByLine[i]));
            }
            return this;
        }

        public CodeStringBuilder InsertAtOpenBracket(string str, int targetOrder, bool isBeforeOrAfter)
        {
            return InsertByOrder(Helper.Indent(IndentLevel, str), "{", targetOrder, isBeforeOrAfter);
        }

        public CodeStringBuilder InsertAtCloseBracket(string str, int targetOrder, bool isBeforeOrAfter)
        {
            return InsertByOrder(Helper.Indent(IndentLevel, str), "}", targetOrder, isBeforeOrAfter);
        }

        public CodeStringBuilder InsertByOrder(string insertStr, string pivotStr, int targetOrder, bool isBeforeOrAfter)
        {
            var curStr = Current.ToString();
            int targetIdx = 0;
            int curOrder = -1;

            while (curOrder < targetOrder)
            {
                targetIdx = curStr.IndexOf(pivotStr, targetIdx);
                curOrder++;

                if (curOrder == targetOrder)
                {
                    if (isBeforeOrAfter)
                        Current.Insert(targetIdx, insertStr);
                    else
                        Current.Insert(targetIdx + 1, insertStr);

                    return this;
                }

                if (targetIdx == -1)
                {
                    return this;
                }
            }

            return this;
        }

        public CodeStringBuilder OpenBracket()
        {
            AppendLine("{");
            IndentLevel++;
            return this;
        }

        public CodeStringBuilder CloseBracket(bool addSemicolon = false)
        {
            IndentLevel--;
            if (addSemicolon)
                AppendLine("};");
            else
                AppendLine("}");
            return this;
        }
    }
}
