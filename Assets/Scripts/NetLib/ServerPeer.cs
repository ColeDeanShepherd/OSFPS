using System;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Networking;

namespace NetworkLibrary
{
    public class ServerPeer : NetworkPeer
    {
        public delegate void ClientConnectionChangeEventHandler(int connectionId);
        public event ClientConnectionChangeEventHandler OnClientConnected;
        public event ClientConnectionChangeEventHandler OnClientDisconnected;

        public delegate void ReceiveDataFromServerHandler(int connectionId, int channelId, byte[] bytesReceived, int numBytesReceived);
        public event ReceiveDataFromServerHandler OnReceiveDataFromClient;
        
        public void Start(ushort? portNumber, int maxPlayerCount)
        {
            var connectionConfig = NetLib.CreateConnectionConfig(
                out reliableSequencedChannelId,
                out reliableChannelId,
                out unreliableStateUpdateChannelId,
                out unreliableFragmentedChannelId,
                out unreliableChannelId
            );
            var hostTopology = new HostTopology(connectionConfig, maxPlayerCount);
            Start(portNumber, hostTopology);
        }
        public override bool Stop()
        {
            var succeeded = base.Stop();

            if (!succeeded)
            {
                OsFps.Logger.LogError("Failed stopping server.");
            }

            return succeeded;
        }

        public NetworkError SendMessageToClientReturnError(int connectionId, int channelId, byte[] messageBytes)
        {
            var networkError = SendMessage(connectionId, channelId, messageBytes);
            if (networkError != NetworkError.Ok)
            {
                OsFps.Logger.LogError(string.Format("Failed sending message to client. Error: {0}", networkError));
            }

            return networkError;
        }
        private void SendMessageToClientHandleErrors(int connectionId, int channelId, byte[] serializedMessage)
        {
            var networkError = SendMessageToClientReturnError(connectionId, channelId, serializedMessage);

            if (networkError != NetworkError.Ok)
            {
                OsFps.Logger.LogError(string.Format("Failed sending message to client. Error: {0}", networkError));
            }
        }
        public void SendMessageToAllClients(int channelId, byte[] serializedMessage)
        {
            foreach (var connectionId in connectionIds)
            {
                SendMessageToClientHandleErrors(connectionId, channelId, serializedMessage);
            }
        }
        public void SendMessageToAllClientsExcept(int exceptConnectionId, int channelId, byte[] serializedMessage)
        {
            foreach (var connectionId in connectionIds)
            {
                if (connectionId == exceptConnectionId) continue;

                SendMessageToClientHandleErrors(connectionId, channelId, serializedMessage);
            }
        }
        public void SendMessageToClient(int connectionId, int channelId, byte[] serializedMessage)
        {
            SendMessageToClientHandleErrors(connectionId, channelId, serializedMessage);
        }

        public void CallRpcOnAllClients(string name, int channelId, object argumentsObj)
        {
            var rpcId = NetLib.rpcIdByName[name];
            var rpcInfo = NetLib.rpcInfoById[rpcId];

            Assert.IsTrue(rpcInfo.ExecuteOn == NetworkPeerType.Client);

            var messageBytes = NetworkSerializationUtils.SerializeRpcCall(rpcInfo, argumentsObj);
            SendMessageToAllClients(channelId, messageBytes);
        }
        public void CallRpcOnAllClientsExcept(string name, int exceptClientConnectionId, int channelId, object argumentsObj)
        {
            var rpcId = NetLib.rpcIdByName[name];
            var rpcInfo = NetLib.rpcInfoById[rpcId];

            Assert.IsTrue(rpcInfo.ExecuteOn == NetworkPeerType.Client);

            var messageBytes = NetworkSerializationUtils.SerializeRpcCall(rpcInfo, argumentsObj);
            SendMessageToAllClientsExcept(exceptClientConnectionId, channelId, messageBytes);
        }
        public void CallRpcOnClient(string name, int clientConnectionId, int channelId, object argumentsObj)
        {
            var rpcId = NetLib.rpcIdByName[name];
            var rpcInfo = NetLib.rpcInfoById[rpcId];

            Assert.IsTrue(rpcInfo.ExecuteOn == NetworkPeerType.Client);

            var messageBytes = NetworkSerializationUtils.SerializeRpcCall(rpcInfo, argumentsObj);
            SendMessageToClient(clientConnectionId, channelId, messageBytes);
        }

        public int? GetRoundTripTimeToClientInMilliseconds(int clientConnectionId)
        {
            if (!socketId.HasValue) return null;

            byte networkErrorAsByte;
            var rttInMs = NetworkTransport.GetCurrentRTT(socketId.Value, clientConnectionId, out networkErrorAsByte);

            var networkError = (NetworkError)networkErrorAsByte;
            return (networkError == NetworkError.Ok) ? rttInMs : (int?)null;
        }
        public float? GetRoundTripTimeToClientInSeconds(int clientConnectionId)
        {
            var rttInMs = GetRoundTripTimeToClientInMilliseconds(clientConnectionId);
            return (rttInMs != null)
                ? ((float)rttInMs.Value / 1000)
                : (float?)null;
        }

        protected override void OnPeerConnected(int connectionId)
        {
            base.OnPeerConnected(connectionId);

            if(OnClientConnected != null)
            {
                OnClientConnected(connectionId);
            }
        }
        protected override void OnPeerDisconnected(int connectionId)
        {
            base.OnPeerDisconnected(connectionId);

            if (OnClientDisconnected != null)
            {
                OnClientDisconnected(connectionId);
            }
        }
        protected override void OnReceiveData(int connectionId, int channelId, byte[] buffer, int numBytesReceived)
        {
            base.OnReceiveData(connectionId, channelId, buffer, numBytesReceived);

            if (OnReceiveDataFromClient != null)
            {
                OnReceiveDataFromClient(connectionId, channelId, buffer, numBytesReceived);
            }
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