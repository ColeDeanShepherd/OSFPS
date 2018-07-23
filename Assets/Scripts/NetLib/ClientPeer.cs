using System;
using UnityEngine.Assertions;
using UnityEngine.Networking;

namespace NetworkLibrary
{
    public class ClientPeer : NetworkPeer
    {
        public delegate void ServerConnectionChangeEventHandler();
        public event ServerConnectionChangeEventHandler OnConnectedToServer;
        public event ServerConnectionChangeEventHandler OnDisconnectedFromServer;

        public delegate void ReceiveDataFromServerHandler(int channelId, byte[] bytesReceived, int numBytesReceived);
        public event ReceiveDataFromServerHandler OnReceiveDataFromServer;

        public int? serverConnectionId;

        public bool IsConnectedToServer
        {
            get
            {
                return serverConnectionId.HasValue;
            }
        }
        public int? RoundTripTimeInMilliseconds
        {
            get
            {
                if (!socketId.HasValue || !serverConnectionId.HasValue) return null;

                byte networkErrorAsByte;
                var rttInMs = NetworkTransport.GetCurrentRTT(socketId.Value, serverConnectionId.Value, out networkErrorAsByte);

                var networkError = (NetworkError)networkErrorAsByte;
                return (networkError == NetworkError.Ok) ? rttInMs : (int?)null;
            }
        }
        public float? RoundTripTimeInSeconds
        {
            get
            {
                var rttInMs = RoundTripTimeInMilliseconds;
                return (rttInMs != null)
                    ? ((float)rttInMs.Value / 1000)
                    : (float?)null;
            }
        }

        public void Start(ConnectionConfig connectionConfig)
        {
            Assert.IsTrue(!IsStarted);

            var maxConnectionCount = 1; // The client only connects to the server.
            var hostTopology = new HostTopology(
                connectionConfig, maxConnectionCount
            );
            Start(null, hostTopology);
        }
        public override bool Stop()
        {
            var succeeded = base.Stop();

            if (!succeeded)
            {
                OsFps.Logger.LogError("Failed stopping client.");
            }

            serverConnectionId = null;

            return succeeded;
        }

        public NetworkError StartConnectingToServer(string serverIpv4Address, ushort serverPortNumber)
        {
            Assert.IsTrue(!IsConnectedToServer);

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
                OsFps.Logger.LogError(string.Format("Failed sending message to server. Error: {0}", networkError));
            }
        }

        public void CallRpcOnServer(string name, int channelId, object argumentsObj)
        {
            var rpcId = NetLib.rpcIdByName[name];
            var rpcInfo = NetLib.rpcInfoById[rpcId];

            Assert.IsTrue(rpcInfo.ExecuteOn == NetworkPeerType.Server);

            var messageBytes = NetworkSerializationUtils.SerializeRpcCall(rpcInfo, argumentsObj);
            SendMessageToServer(channelId, messageBytes);
        }

        protected override void OnPeerConnected(int connectionId)
        {
            base.OnPeerConnected(connectionId);

            serverConnectionId = connectionId;

            if(OnConnectedToServer != null)
            {
                OnConnectedToServer();
            }
        }
        protected override void OnPeerDisconnected(int connectionId)
        {
            base.OnPeerConnected(connectionId);
            serverConnectionId = null;

            if(OnDisconnectedFromServer != null)
            {
                OnDisconnectedFromServer();
            }
        }
        protected override void OnReceiveData(int connectionId, int channelId, byte[] buffer, int numBytesReceived)
        {
            base.OnReceiveData(connectionId, channelId, buffer, numBytesReceived);
            OnReceiveDataFromServer(channelId, buffer, numBytesReceived);
        }
        protected override void OnNetworkErrorEvent(int connectionId, int channelId, NetworkError error, NetworkEventType eventType, byte[] buffer, int numBytesReceived)
        {
            base.OnNetworkErrorEvent(connectionId, channelId, error, eventType, buffer, numBytesReceived);

            var errorMessage = string.Format(
                    "Network error. Error: {0}. Event Type: {1}",
                    error, eventType
                );
            OsFps.Logger.LogError(errorMessage);
        }
    }
}