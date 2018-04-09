using UnityEngine;
using UnityEngine.Networking;

public class NetworkPeer
{
    public int? socketId;
    public int reliableSequencedChannelId;
    
    public bool IsStarted
    {
        get
        {
            return socketId.HasValue;
        }
    }

    public ConnectionConfig CreateConnectionConfig()
    {
        var connectionConfig = new ConnectionConfig();
        reliableSequencedChannelId = connectionConfig.AddChannel(QosType.ReliableSequenced);

        return connectionConfig;
    }
    public virtual bool Stop()
    {
        Debug.Assert(IsStarted);

        var succeeded = NetworkTransport.RemoveHost(socketId.Value);

        socketId = null;
        reliableSequencedChannelId = 0;

        return succeeded;
    }

    public void HandleNetworkEvent(
        NetworkEventType networkEventType, int connectionId, int channelId,
        byte[] buffer, int numBytesReceived, NetworkError networkError
    )
    {
        if (networkError != NetworkError.Ok)
        {
            var errorMessage = string.Format(
                "Failed receiving a message. Error: {0}. Event Type: {1}",
                networkError, networkEventType
            );
            Debug.LogError(errorMessage);

            return;
        }

        switch (networkEventType)
        {
            case NetworkEventType.BroadcastEvent:
                OnReceiveBroadcast();
                break;
            case NetworkEventType.ConnectEvent:
                OnConnect(connectionId);
                break;
            case NetworkEventType.DisconnectEvent:
                OnDisconnect(connectionId);
                break;
            case NetworkEventType.DataEvent:
                OnReceiveData(connectionId, channelId, buffer, numBytesReceived);
                break;
            default:
                var errorMessage = string.Format(
                    "Unknown network message type: {0}", networkEventType
                );
                Debug.LogError(errorMessage);

                break;
        }
    }

    protected virtual void OnReceiveBroadcast() { }
    protected virtual void OnConnect(int connectionId) { }
    protected virtual void OnDisconnect(int connectionId) { }
    protected virtual void OnReceiveData(int connectionId, int channelId, byte[] buffer, int numBytesReceived) { }
}