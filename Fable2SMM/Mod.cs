using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fable2SMM
{
    class Mod
    {
        public string ModID { get; set; }
        public string ModName { get; set; }
        public string ModDescription { get; set; }
        public string ModAuthor { get; set; }
        public bool Enabled { get; set; }
        public bool Installed { get; set; }
        public string[] Files { get; set; }
        public string Directory { get; set; }
    }
}
