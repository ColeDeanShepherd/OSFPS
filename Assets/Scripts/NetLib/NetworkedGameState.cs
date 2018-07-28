using System.Collections.Generic;

namespace NetworkLibrary
{
    public struct NetworkedGameState
    {
        public uint SequenceNumber;
        public List<NetworkedComponentTypeInfo> NetworkedComponentTypeInfos;
        public List<List<NetworkedComponentInfo>> NetworkedComponentInfoLists;
    }
}