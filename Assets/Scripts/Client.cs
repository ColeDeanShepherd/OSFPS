using UnityEngine;
using UnityEngine.Networking;

public class Client : NetworkPeer
{
    public int? serverConnectionId;
    
    public bool IsConnectedToServer
    {
        get
        {
            return serverConnectionId.HasValue;
        }
    }

    public void Start()
    {
        Debug.Assert(!IsStarted);

        var maxConnectionCount = 1; // The client only connects to the server.
        var hostTopology = new HostTopology(
            CreateConnectionConfig(), maxConnectionCount
        );
        socketId = NetworkTransport.AddHost(hostTopology);
    }
    public override bool Stop()
    {
        var succeeded = base.Stop();

        if (!succeeded)
        {
            Debug.Log("Failed stopping client.");
        }
        
        serverConnectionId = null;

        return succeeded;
    }

    public void StartConnectingToServer(string serverIpv4Address)
    {
        Debug.Assert(!IsConnectedToServer);

        var exceptionConnectionId = 0;
        byte networkErrorAsByte;
        NetworkTransport.Connect(
            socketId.Value, serverIpv4Address, Server.PortNumber, exceptionConnectionId,
            out networkErrorAsByte
        );

        var networkError = (NetworkError)networkErrorAsByte;
        if (networkError != NetworkError.Ok)
        {
            var errorMessage = string.Format(
                "Failed connecting to server {0}:{1}. Error: {2}",
                serverIpv4Address, Server.PortNumber, networkError
            );
            Debug.LogError(errorMessage);

            return;
        }
    }
    public void DisconnectFromServer()
    {
        Debug.Assert(IsConnectedToServer);

        byte networkErrorAsByte;
        var mysteryReturnedBool = NetworkTransport.Disconnect(
            socketId.Value, serverConnectionId.Value, out networkErrorAsByte
        );

        var networkError = (NetworkError)networkErrorAsByte;
        if (networkError != NetworkError.Ok)
        {
            Debug.LogError(string.Format("Failed disconnecting from server. Error: {0}", networkError));
        }
        
        serverConnectionId = null;
    }

    public void SendMessageToServer(int channelId, byte[] messageBytes)
    {
        byte networkErrorAsByte;
        var mysteryReturnedBool = NetworkTransport.Send(
            socketId.Value, serverConnectionId.Value, reliableSequencedChannelId,
            messageBytes, messageBytes.Length, out networkErrorAsByte
        );

        var networkError = (NetworkError)networkErrorAsByte;
        if (networkError != NetworkError.Ok)
        {
            Debug.LogError(string.Format("Failed sending message to server. Error: {0}", networkError));
        }
    }

    protected override void OnConnect(int connectionId)
    {
        Debug.Log("ClientOnConnect " + connectionId);

        serverConnectionId = connectionId;
    }
    protected override void OnDisconnect(int connectionId)
    {
        Debug.Log("ClientOnDisconnect " + connectionId);

        serverConnectionId = null;
    }
    protected override void OnReceiveData(int connectionId, int channelId, byte[] buffer, int numBytesReceived)
    {
        Debug.Log("ClientOnReceivedData");
    }
}