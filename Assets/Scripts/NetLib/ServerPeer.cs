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
                Debug.Log("Failed stopping server.");
            }

            return succeeded;
        }

        public void SendMessageToClient(int connectionId, int channelId, byte[] messageBytes)
        {
            byte networkErrorAsByte;
            var mysteryReturnedBool = NetworkTransport.Send(
                socketId.Value, connectionId, channelId,
                messageBytes, messageBytes.Length, out networkErrorAsByte
            );

            var networkError = (NetworkError)networkErrorAsByte;
            if (networkError != NetworkError.Ok)
            {
                Debug.LogError(string.Format("Failed sending message to client. Error: {0}", networkError));
            }
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
    }
}