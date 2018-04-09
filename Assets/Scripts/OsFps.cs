using UnityEngine;
using UnityEngine.Networking;

public class OsFps : MonoBehaviour
{
    public const int ReceiveBufferSize = 1024;
    public const string LocalHostIpv4Address = "127.0.0.1";
    
    public Server server;
    public Client client;

    private void Start()
    {
        // Initialize & configure network.
        NetworkTransport.Init();

        server = new Server();
        server.Start();

        client = new Client();
        client.Start();

        client.StartConnectingToServer(LocalHostIpv4Address);
    }
    private void OnDestroy()
    {
        client.DisconnectFromServer();
        client.Stop();

        server.Stop();
        
        NetworkTransport.Shutdown();
    }
    private void Update()
    {
        ReceiveAndHandleMessages();
    }

    private byte[] _netReceiveBuffer = new byte[ReceiveBufferSize];
    private void ReceiveAndHandleMessages()
    {
        while(true)
        {
            int socketId;
            int connectionId;
            int channelId;
            int numBytesReceived;
            byte networkErrorAsByte;
            var networkEventType = NetworkTransport.Receive(
               out socketId, out connectionId, out channelId, _netReceiveBuffer,
               _netReceiveBuffer.Length, out numBytesReceived, out networkErrorAsByte
            );

            if (networkEventType == NetworkEventType.Nothing)
            {
                break;
            }

            var networkError = (NetworkError)networkErrorAsByte;
            var isServer = (server != null) && (socketId == server.socketId);
            var isClient = (client != null) && (socketId == client.socketId);

            if(isServer)
            {
                server.HandleNetworkEvent(
                    networkEventType, connectionId, channelId,
                    _netReceiveBuffer, numBytesReceived, networkError
                );
            }
            else if(isClient)
            {
                client.HandleNetworkEvent(
                    networkEventType, connectionId, channelId,
                    _netReceiveBuffer, numBytesReceived, networkError
                );
            }
        }
    }
}