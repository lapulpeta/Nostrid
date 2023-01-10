using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nostrid.Misc
{
    internal class IdGenerator
    {
        private static Random random = new Random();
        private const string characters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        private static string GenerateRandomString(int length)
        {
            // Use the Random class to generate a random number for each character
            return new string(
                Enumerable.Repeat(characters, length)
                          .Select(s => s[random.Next(s.Length)])
                          .ToArray());
        }

        public static string Generate()
        {
            return GenerateRandomString(20);
        }
    }
}
