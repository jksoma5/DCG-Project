using System;
using DCG.Core;

namespace DCG.Networking
{
    [Serializable]
    public sealed class DuelMessage
    {
        public string kind;
        public int round, value;
        public bool confirm;
        public DuelState state;
    }
}
