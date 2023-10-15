using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArchonsToolbox
{
    public enum GamescriptsStatus
    {
        INVALIDSTATE = 0,
        ORIGINAL = 1,
        MANAGERINSTALLED = 2,
        MODIFIED = 4,
        MISSING = 8
    }
}
