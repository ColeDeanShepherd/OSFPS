using System;
using UnityEngine;
using UnityEngine.Networking;

namespace NetLib
{
    public class ServerPeer : NetworkPeer
    {
        public delegate void ClientConnectionChangeEventHandler(int connectionId);
        public event ClientConnectionChangeEventHandler OnClientConnected;
        public event ClientConnectionChangeEventHandler OnClientDisconnected;

        public delegate void ReceiveDataFromServerHandler(int connectionId, int channelId, byte[] bytesReceived);
        public event ReceiveDataFromServerHandler OnReceiveDataFromClient;
        
        public override bool Stop()
        {
            var succeeded = base.Stop();

            if (!succeeded)
            {
                OsFps.Logger.LogError("Failed stopping server.");
            }

            return succeeded;
        }

        public NetworkError SendMessageToClient(int connectionId, int channelId, byte[] messageBytes)
        {
            var networkError = SendMessage(connectionId, channelId, messageBytes);
            if (networkError != NetworkError.Ok)
            {
                OsFps.Logger.LogError(string.Format("Failed sending message to client. Error: {0}", networkError));
            }

            return networkError;
        }

        protected override void OnPeerConnected(int connectionId)
        {
            if(OnClientConnected != null)
            {
                OnClientConnected(connectionId);
            }
        }
        protected override void OnPeerDisconnected(int connectionId)
        {
            if(OnClientDisconnected != null)
            {
                OnClientDisconnected(connectionId);
            }
        }
        protected override void OnReceiveData(int connectionId, int channelId, byte[] buffer, int numBytesReceived)
        {
            var bytesReceived = new byte[numBytesReceived];
            Array.Copy(buffer, bytesReceived, numBytesReceived);

            if (OnReceiveDataFromClient != null)
            {
                OnReceiveDataFromClient(connectionId, channelId, bytesReceived);
            }
        }
        protected override void OnNetworkErrorEvent(int connectionId, int channelId, NetworkError error, NetworkEventType eventType, byte[] buffer, int numBytesReceived)
        {
            var errorMessage = string.Format(
                "Network error. Error: {0}. Event Type: {1}",
                error, eventType
            );
            OsFps.Logger.LogError(errorMessage);
        }

    }
}