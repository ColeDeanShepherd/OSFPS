using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Networking;
using UnityEngine.Profiling;

namespace NetworkLibrary
{
    public class NetworkPeer
    {
        public const int ReceiveBufferSize = 65535;

        public int? socketId;
        public List<int> connectionIds;

        public bool IsStarted
        {
            get
            {
                return socketId.HasValue;
            }
        }

        public virtual void Start(ushort? portNumber, HostTopology hostTopology)
        {
            Assert.IsTrue(!IsStarted);

            if (portNumber.HasValue)
            {
                socketId = NetworkTransport.AddHost(hostTopology, portNumber.Value);
            }
            else
            {
                socketId = NetworkTransport.AddHost(hostTopology);
            }

            connectionIds = new List<int>();

            bandwidthAverager = new BandwidthMovingAverager();
        }
        public virtual bool Stop()
        {
            Assert.IsTrue(IsStarted);

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

                Profiler.BeginSample("NetworkTransport.ReceiveFromHost");
                int connectionId;
                int channelId;
                int numBytesReceived;
                byte networkErrorAsByte;
                var networkEventType = NetworkTransport.ReceiveFromHost(
                   socketId.Value, out connectionId, out channelId, _netReceiveBuffer,
                   _netReceiveBuffer.Length, out numBytesReceived, out networkErrorAsByte
                );
                Profiler.EndSample();

                if (networkEventType == NetworkEventType.Nothing)
                {
                    break;
                }

                var networkError = (NetworkError)networkErrorAsByte;

                if (networkError == NetworkError.MessageToLong)
                {
                    throw new System.Exception("A message was too long for the read buffer!");
                }

                Profiler.BeginSample("HandleNetworkEvent");
                HandleNetworkEvent(
                    networkEventType, connectionId, channelId,
                    _netReceiveBuffer, numBytesReceived, networkError
                );
                Profiler.EndSample();
            }
        }

        public void Update()
        {
            Profiler.BeginSample("ReceiveAndHandleNetworkEvents");
            ReceiveAndHandleNetworkEvents();
            Profiler.EndSample();

            bandwidthAverager.Update();
        }

        public NetworkStats GetNetworkStats(int? connectionId)
        {
            byte errorAsByte;

            var networkStats = new NetworkStats
            {
                RecentOutgoingBandwidthInBytes = bandwidthAverager.OutgoingBandwidthInBytes,
                IncomingPacketCountForAllHosts = NetworkTransport.GetIncomingPacketCountForAllHosts(),
                IncomingPacketDropCountForAllHosts = NetworkTransport.GetIncomingPacketDropCountForAllHosts(),
                NetworkTimestamp = NetworkTransport.GetNetworkTimestamp(),
                OutgoingFullBytesCount = NetworkTransport.GetOutgoingFullBytesCount(),
                OutgoingMessageCount = NetworkTransport.GetOutgoingMessageCount(),
                OutgoingPacketCount = NetworkTransport.GetOutgoingPacketCount(),
                OutgoingSystemBytesCount = NetworkTransport.GetOutgoingSystemBytesCount(),
                OutgoingUserBytesCount = NetworkTransport.GetOutgoingUserBytesCount(),
            };

            if (socketId.HasValue)
            {
                networkStats.IncomingMessageQueueSize = NetworkTransport.GetIncomingMessageQueueSize(socketId.Value, out errorAsByte);
                networkStats.OutgoingFullBytesCountForHost = NetworkTransport.GetOutgoingFullBytesCountForHost(socketId.Value, out errorAsByte);
                networkStats.OutgoingMessageCountForHost = NetworkTransport.GetOutgoingMessageCountForHost(socketId.Value, out errorAsByte);
                networkStats.OutgoingMessageQueueSize = NetworkTransport.GetOutgoingMessageQueueSize(socketId.Value, out errorAsByte);
                networkStats.OutgoingPacketCountForHost = NetworkTransport.GetOutgoingPacketCountForHost(socketId.Value, out errorAsByte);
                networkStats.OutgoingSystemBytesCountForHost = NetworkTransport.GetOutgoingSystemBytesCountForHost(socketId.Value, out errorAsByte);
                networkStats.OutgoingUserBytesCountForHost = NetworkTransport.GetOutgoingUserBytesCountForHost(socketId.Value, out errorAsByte);

                if (connectionId.HasValue)
                {
                    networkStats.AckBufferCount = NetworkTransport.GetAckBufferCount(socketId.Value, connectionId.Value, out errorAsByte);
                    networkStats.CurrentRTT = NetworkTransport.GetCurrentRTT(socketId.Value, connectionId.Value, out errorAsByte);
                    networkStats.IncomingPacketCount = NetworkTransport.GetIncomingPacketCount(socketId.Value, connectionId.Value, out errorAsByte);
                    networkStats.IncomingPacketLossCount = NetworkTransport.GetIncomingPacketLossCount(socketId.Value, connectionId.Value, out errorAsByte);
                    networkStats.MaxAllowedBandwidth = NetworkTransport.GetMaxAllowedBandwidth(socketId.Value, connectionId.Value, out errorAsByte);
                    networkStats.OutgoingFullBytesCountForConnection = NetworkTransport.GetOutgoingFullBytesCountForConnection(socketId.Value, connectionId.Value, out errorAsByte);
                    networkStats.OutgoingMessageCountForConnection = NetworkTransport.GetOutgoingMessageCountForConnection(socketId.Value, connectionId.Value, out errorAsByte);
                    networkStats.OutgoingPacketCountForConnection = NetworkTransport.GetOutgoingPacketCountForConnection(socketId.Value, connectionId.Value, out errorAsByte);
                    networkStats.OutgoingPacketNetworkLossPercent = NetworkTransport.GetOutgoingPacketNetworkLossPercent(socketId.Value, connectionId.Value, out errorAsByte);
                    networkStats.OutgoingPacketOverflowLossPercent = NetworkTransport.GetOutgoingPacketOverflowLossPercent(socketId.Value, connectionId.Value, out errorAsByte);
                    networkStats.OutgoingSystemBytesCountForConnection = NetworkTransport.GetOutgoingSystemBytesCountForConnection(socketId.Value, connectionId.Value, out errorAsByte);
                    networkStats.OutgoingUserBytesCountForConnection = NetworkTransport.GetOutgoingUserBytesCountForConnection(socketId.Value, connectionId.Value, out errorAsByte);
                }
            }

            return networkStats;
        }

        protected virtual void OnReceiveBroadcast() { }
        protected virtual void OnPeerConnected(int connectionId)
        {
            connectionIds.Add(connectionId);
        }
        protected virtual void OnPeerDisconnected(int connectionId)
        {
            connectionIds.Remove(connectionId);
        }
        protected virtual void OnReceiveData(int connectionId, int channelId, byte[] buffer, int numBytesReceived) { }
        protected virtual void OnNetworkErrorEvent(int connectionId, int channelId, NetworkError error, NetworkEventType eventType, byte[] buffer, int numBytesReceived) { }

        private BandwidthMovingAverager bandwidthAverager;

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
                    OsFps.Logger.LogError(errorMessage);

                    break;
            }
        }
    }
}