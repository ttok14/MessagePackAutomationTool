using System;
using System.Collections.Generic;
using System.Text;

namespace MSgPackBinaryGenerator
{
    public static class StringBuilderExtensions
    {
        public static int IndexOf(this StringBuilder sb, string value)
        {
            return IndexOf(sb, value, 0);
        }

        public static int IndexOf(this StringBuilder sb, string value, int startIndex)
        {
            if (sb == null || value == null || value.Length == 0)
                return -1;

            if (startIndex < 0 || startIndex >= sb.Length)
                return -1; 

            int searchLength = value.Length;
            int maxSearchStartIndex = sb.Length - searchLength;

            if (startIndex > maxSearchStartIndex)
                return -1;

            for (int i = startIndex; i <= maxSearchStartIndex; i++)
            {
                bool match = true;
                for (int j = 0; j < searchLength; j++)
                {
                    if (sb[i + j] != value[j])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
