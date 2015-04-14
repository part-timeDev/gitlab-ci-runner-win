using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace gitlab_ci_runner.helper
{
    class PathHelper
    {
        public static string makeValidPath(string unescaped)
        {
            return unescaped.Replace(" ", "")
                            .Replace(",", "_")
                            .Replace(".", "_")
                            .Replace("\"", "_")
                            .Replace("\\", "_")
                            .Replace("/", "_")
                            .Replace("'", "_")
                            .Replace("%", "_")
                            .Replace("]", "_")
                            .Replace("[", "_");
        }
    }
}
