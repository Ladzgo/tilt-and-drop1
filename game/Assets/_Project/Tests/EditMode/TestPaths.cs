using System;
using System.IO;

namespace TiltDrop.Core.Tests
{
    static class TestPaths
    {
        /// <summary>Finds a repo folder (e.g. "shared/golden") walking up from the working and base directories.</summary>
        public static string Find(string relative)
        {
            foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                var dir = new DirectoryInfo(start);
                while (dir != null)
                {
                    var p = Path.Combine(dir.FullName, relative);
                    if (Directory.Exists(p) || File.Exists(p)) return p;
                    dir = dir.Parent;
                }
            }
            return null;
        }
    }
}
