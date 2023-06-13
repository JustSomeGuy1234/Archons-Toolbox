using System;
using System.Text.RegularExpressions;
using System.Net.Http;
using System.Net;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Xml.Serialization;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.IO.Compression;

namespace Fable2SMM
{

    /* This class was meant to be so you could add a list of operations and then do them all at once without having to re-parse the table after each modification, but just parsing the table every time a value changes is fine really.
    public class TableModification
    {
        public TableModification(OperationType Action, string Key, int ValuePosition, object ValueReplacement = null)
        {
            this.Action = Action;
            this.Key = Key;
            this.ValuePosition = ValuePosition;
            this.ValueReplacement = ValueReplacement;
        }

        public int CompareTo(TableModification a, TableModification b)
        {
            if (a == null)
                return 1;
            if (b == null)
                return -1;

            if (a.ValuePosition < b.ValuePosition)
                return -1;
            else
                return 1;

        }

        public enum OperationType
        {
            ACTION_REPLACE_STRING = 0,
            ACTION_REPLACE_NUM = 1,
            ACTION_REPLACE_BOOL = 2,
            ACTION_REMOVE_TABLE = 4,
        }

        public OperationType Action { get { return _action; } set { _action = value; }  }
        OperationType _action;
        public string Key { get { return _key; } set { _key = value; } }
        string _key;
        public int ValuePosition { get { return _valuePosition; } set { _valuePosition = value; } }
        int _valuePosition = -1;
        public object ValueReplacement { get { return _valueReplacement; } set { _valueReplacement = value; } }
        object _valueReplacement;
    }
    */



}