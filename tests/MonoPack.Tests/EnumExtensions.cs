using System.Globalization;
using System.Text;

namespace MonoPack.Tests;

internal static class EnumExtensions
{
    public static string PrintFlags<T>(this T value) where T : Enum
    {
        StringBuilder sb = new StringBuilder();
        foreach (T flag in Enum.GetValues(typeof(T)))
        {
            if (Convert.ToInt64(flag, CultureInfo.InvariantCulture) != 0 && value.HasFlag(flag))
            {
                sb.Append(flag.ToString() + ",");
            }
        }
        return sb.ToString().TrimEnd(',');
    }
}
