using UnityEngine;
using UnityEngine.Networking;

namespace NetLib
{
    public class NetworkPeer
    {
        public const int ReceiveBufferSize = 1024;

        public int? socketId;

        public bool IsStarted
        {
            get
            {
                return socketId.HasValue;
            }
        }

        public virtual void Start(ushort? portNumber, HostTopology hostTopology)
        {
            Debug.Assert(!IsStarted);

            if (portNumber.HasValue)
            {
                socketId = NetworkTransport.AddHost(hostTopology, portNumber.Value);
            }
            else
            {
                socketId = NetworkTransport.AddHost(hostTopology);
            }
        }
        public virtual bool Stop()
        {
            Debug.Assert(IsStarted);

            var succeeded = NetworkTransport.RemoveHost(socketId.Value);

            socketId = null;

            return succeeded;
        }

        public NetworkError SendMessage(int connectionId, int channelId, byte[] messageBytes)
        {
            byte networkErrorAsByte;
            var mysteryReturnedBool = NetworkTransport.Send(
                socketId.Value, connectionId, channelId,
                messageBytes, messageBytes.Length, out networkErrorAsByte
            );

            var networkError = (NetworkError)networkErrorAsByte;
            return networkError;
        }

        private byte[] _netReceiveBuffer = new byte[ReceiveBufferSize];
        public void ReceiveAndHandleNetworkEvents()
        {
            while (true)
            {
                if (!socketId.HasValue) return;

                int connectionId;
                int channelId;
                int numBytesReceived;
                byte networkErrorAsByte;
                var networkEventType = NetworkTransport.ReceiveFromHost(
                   socketId.Value, out connectionId, out channelId, _netReceiveBuffer,
                   _netReceiveBuffer.Length, out numBytesReceived, out networkErrorAsByte
                );

                if (networkEventType == NetworkEventType.Nothing)
                {
                    break;
                }

                var networkError = (NetworkError)networkErrorAsByte;

                HandleNetworkEvent(
                    networkEventType, connectionId, channelId,
                    _netReceiveBuffer, numBytesReceived, networkError
                );
            }
        }

        protected virtual void OnReceiveBroadcast() { }
        protected virtual void OnPeerConnected(int connectionId) { }
        protected virtual void OnPeerDisconnected(int connectionId) { }
        protected virtual void OnReceiveData(int connectionId, int channelId, byte[] buffer, int numBytesReceived) { }
        protected virtual void OnNetworkErrorEvent(int connectionId, int channelId, NetworkError error, NetworkEventType eventType, byte[] buffer, int numBytesReceived) { }

        private void HandleNetworkEvent(
            NetworkEventType networkEventType, int connectionId, int channelId,
            byte[] buffer, int numBytesReceived, NetworkError networkError
        )
        {
            if (networkError != NetworkError.Ok)
            {
                if ((networkError == NetworkError.WrongConnection) || (networkError == NetworkError.Timeout))
                {
                    OnPeerDisconnected(connectionId);
                }
                else
                {
                    OnNetworkErrorEvent(connectionId, channelId, networkError, networkEventType, buffer, numBytesReceived);
                }

                return;
            }

            switch (networkEventType)
            {
                case NetworkEventType.BroadcastEvent:
                    OnReceiveBroadcast();
                    break;
                case NetworkEventType.ConnectEvent:
                    OnPeerConnected(connectionId);
                    break;
                case NetworkEventType.DisconnectEvent:
                    OnPeerDisconnected(connectionId);
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
    }
}