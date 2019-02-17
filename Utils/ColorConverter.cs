using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;

namespace CheckStaging.Utils
{
    public static class ColorConverter
    {
        public static string ToHtml(this Color color)
        {
            var result = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            return result;
        }
    }
}
