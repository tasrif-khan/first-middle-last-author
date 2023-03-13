using System;
using System.Collections.Generic;

namespace Research_Author_Publication_Data
{
    public static class StringExtensions
    {
        public static string[] SubstringsOrEmpty(this string self, string left, string right,
               int startIndex = 0, StringComparison comparison = StringComparison.Ordinal, int limit = 0)
        {
            #region Parameter Check
            if (string.IsNullOrEmpty(self))
                return new string[0];

            if (string.IsNullOrEmpty(left))
                throw new ArgumentNullException(nameof(left));

            if (string.IsNullOrEmpty(right))
                throw new ArgumentNullException(nameof(right));

            if (startIndex < 0 || startIndex >= self.Length)
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            #endregion

            int currentStartIndex = startIndex;
            int current = limit;
            var strings = new List<string>();

            while (true)
            {
                if (limit > 0)
                {
                    --current;
                    if (current < 0)
                        break;
                }

                int leftPosBegin = self.IndexOf(left, currentStartIndex, comparison);
                if (leftPosBegin == -1)
                    break;

                int leftPosEnd = leftPosBegin + left.Length;
                int rightPos = self.IndexOf(right, leftPosEnd, comparison);
                if (rightPos == -1)
                    break;

                int length = rightPos - leftPosEnd;
                strings.Add(self.Substring(leftPosEnd, length));
                currentStartIndex = rightPos + right.Length;
            }

            return strings.ToArray();
        }

        public static string[] Substrings(this string self, string left, string right,
            int startIndex = 0, StringComparison comparison = StringComparison.Ordinal, int limit = 0, string[] fallback = null)
        {
            var result = SubstringsOrEmpty(self, left, right, startIndex, comparison, limit);

            return result.Length > 0 ? result : fallback;
        }

        public static string Substring(this string self, string left, string right,
            int startIndex = 0, StringComparison comparison = StringComparison.Ordinal, string fallback = null)
        {
            if (string.IsNullOrEmpty(self) || string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right) ||
                startIndex < 0 || startIndex >= self.Length)
                return fallback;

            int leftPosBegin = self.IndexOf(left, startIndex, comparison);
            if (leftPosBegin == -1)
                return fallback;

            int leftPosEnd = leftPosBegin + left.Length;
            int rightPos = self.IndexOf(right, leftPosEnd, comparison);

            return rightPos != -1 ? self.Substring(leftPosEnd, rightPos - leftPosEnd) : fallback;
        }

    }
}
