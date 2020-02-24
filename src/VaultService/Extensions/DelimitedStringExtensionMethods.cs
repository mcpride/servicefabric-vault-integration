using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VaultService.Extensions
{
    public static class DelimitedStringExtensionMethods
    {
        public static string[] DelimitedBy(this string value, char delimiter)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(value)) return result.ToArray();
            var segments = value.Split(delimiter);
            result.AddRange(segments.Where(segment => !string.IsNullOrEmpty(segment)));
            return result.ToArray();
        }

        public static string EnsureTrailingDelimiter(this string value, char delimiter)
        {
            if (string.IsNullOrEmpty(value)) return $"{delimiter}";
            var c = value[value.Length - 1];
            return c == delimiter ? value : $"{value}{delimiter}";
        }

        public static string RemoveTrailingDelimiter(this string value, char delimiter)
        {
            if (string.IsNullOrEmpty(value)) return value;
            var c = value[value.Length - 1];
            return c != delimiter ? value : value.Substring(0, value.Length - 1);
        }

        public static string ParentDelimitedBy(this string value, char delimiter)
        {
            var segments = value.DelimitedBy(delimiter);
            var sb = new StringBuilder();
            if (segments.Length == 1)
            {
                sb.Append(delimiter);
            }
            else
            {
                for (var i = 0; i < segments.Length - 1; i++)
                {
                    sb.Append(segments[i]);
                    sb.Append(delimiter);
                }
            }
            return sb.ToString();
        }
    }
}
