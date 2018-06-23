namespace NetworkLibrary
{
    public struct RpcInfo
    {
        public byte Id;
        public string Name;
        public NetworkPeerType ExecuteOn;
        public System.Reflection.MethodInfo MethodInfo;
        public string[] ParameterNames;
        public System.Type[] ParameterTypes;
    }
}