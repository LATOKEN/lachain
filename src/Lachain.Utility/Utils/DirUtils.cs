using System.IO;
using System.Linq;

namespace Lachain.Utility.Utils
{
    public class DirUtils
    {
        public static long DirSize(DirectoryInfo d)
        {
            var size = d.GetFiles().Sum(fi => fi.Length);
            size += d.GetDirectories().Sum(DirSize);
            return size;
        }
    }
}