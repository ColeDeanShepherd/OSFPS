using System;
using UnityEngine;
using UnityEngine.Networking;

namespace NetLib
{
    public class ClientPeer : NetworkPeer
    {
        public delegate void ServerConnectionChangeEventHandler();
        public event ServerConnectionChangeEventHandler OnConnectedToServer;
        public event ServerConnectionChangeEventHandler OnDisconnectedFromServer;

        public delegate void ReceiveDataFromServerHandler(int channelId, byte[] bytesReceived);
        public event ReceiveDataFromServerHandler OnReceiveDataFromServer;

        public int? serverConnectionId;

        public bool IsConnectedToServer
        {
            get
            {
                return serverConnectionId.HasValue;
            }
        }

        public void Start(ConnectionConfig connectionConfig)
        {
            Debug.Assert(!IsStarted);

            var maxConnectionCount = 1; // The client only connects to the server.
            var hostTopology = new HostTopology(
                connectionConfig, maxConnectionCount
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

        public NetworkError StartConnectingToServer(string serverIpv4Address, ushort serverPortNumber)
        {
            Debug.Assert(!IsConnectedToServer);

            var exceptionConnectionId = 0;
            byte networkErrorAsByte;
            NetworkTransport.Connect(
                socketId.Value, serverIpv4Address, serverPortNumber, exceptionConnectionId,
                out networkErrorAsByte
            );

            var networkError = (NetworkError)networkErrorAsByte;
            return networkError;
        }
        public NetworkError DisconnectFromServer()
        {
            if (!IsConnectedToServer) return NetworkError.Ok;

            byte networkErrorAsByte;
            var mysteryReturnedBool = NetworkTransport.Disconnect(
                socketId.Value, serverConnectionId.Value, out networkErrorAsByte
            );

            var networkError = (NetworkError)networkErrorAsByte;
            serverConnectionId = null;

            return networkError;
        }

        public void SendMessageToServer(int channelId, byte[] messageBytes)
        {
            var networkError = SendMessage(serverConnectionId.Value, channelId, messageBytes);
            if (networkError != NetworkError.Ok)
            {
                Debug.LogError(string.Format("Failed sending message to server. Error: {0}", networkError));
            }
        }

        protected override void OnPeerConnected(int connectionId)
        {
            serverConnectionId = connectionId;

            if(OnConnectedToServer != null)
            {
                OnConnectedToServer();
            }
        }
        protected override void OnPeerDisconnected(int connectionId)
        {
            serverConnectionId = null;

            if(OnDisconnectedFromServer != null)
            {
                OnDisconnectedFromServer();
            }
        }
        protected override void OnReceiveData(int connectionId, int channelId, byte[] buffer, int numBytesReceived)
        {
            var bytesReceived = new byte[numBytesReceived];
            Array.Copy(buffer, bytesReceived, numBytesReceived);

            OnReceiveDataFromServer(channelId, bytesReceived);
        }
        protected override void OnNetworkErrorEvent(int connectionId, int channelId, NetworkError error, NetworkEventType eventType, byte[] buffer, int numBytesReceived)
        {
            var errorMessage = string.Format(
                    "Network error. Error: {0}. Event Type: {1}",
                    error, eventType
                );
            Debug.LogError(errorMessage);
        }
    }
}