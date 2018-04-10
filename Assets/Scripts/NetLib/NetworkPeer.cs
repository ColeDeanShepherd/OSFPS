using UnityEngine;
using UnityEngine.Networking;

namespace NetLib
{
    public class NetworkPeer
    {
        public const int ReceiveBufferSize = 1024;

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
        public virtual void Start(ushort? portNumber, uint maxConnectionCount)
        {
            Debug.Assert(!IsStarted);

            var hostTopology = new HostTopology(
                CreateConnectionConfig(), (int)maxConnectionCount
            );

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
            reliableSequencedChannelId = 0;

            return succeeded;
        }

        private byte[] _netReceiveBuffer = new byte[ReceiveBufferSize];
        public void ReceiveAndHandleNetworkEvents()
        {
            while (true)
            {
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

        private void HandleNetworkEvent(
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