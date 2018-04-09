using UnityEngine;
using UnityEngine.Networking;

public class Server : NetworkPeer
{
    public const int MaxPlayerCount = 4;
    public const int PortNumber = 32321;

    public void Start()
    {
        Debug.Assert(!IsStarted);

        var hostTopology = new HostTopology(
            CreateConnectionConfig(), MaxPlayerCount
        );
        socketId = NetworkTransport.AddHost(hostTopology, PortNumber);
    }
    public override bool Stop()
    {
        var succeeded = base.Stop();

        if(!succeeded)
        {
            Debug.Log("Failed stopping server.");
        }

        return succeeded;
    }
    
    protected override void OnConnect(int connectionId)
    {
        Debug.Log("ServerOnConnect " + connectionId);
    }
    protected override void OnDisconnect(int connectionId)
    {
        Debug.Log("ServerOnDisconnect " + connectionId);
    }
    protected override void OnReceiveData(int connectionId, int channelId, byte[] buffer, int numBytesReceived)
    {
        Debug.Log("ServerOnReceivedData");
    }
}