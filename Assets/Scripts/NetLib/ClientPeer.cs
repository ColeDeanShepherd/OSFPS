using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using UnityEngine.Assertions;
using UnityEngine.Networking;
using UnityEngine.Profiling;

namespace NetworkLibrary
{
    public class ClientPeer : NetworkPeer
    {
        public delegate void ServerConnectionChangeEventHandler();
        public event ServerConnectionChangeEventHandler OnConnectedToServer;
        public event ServerConnectionChangeEventHandler OnDisconnectedFromServer;

        public int? ServerConnectionId;
        public object ObjectContainingRpcs;
        public bool ShouldApplyStateSnapshots;

        public bool IsConnectedToServer
        {
            get
            {
                return ServerConnectionId.HasValue;
            }
        }
        public int? RoundTripTimeInMilliseconds
        {
            get
            {
                if (!socketId.HasValue || !ServerConnectionId.HasValue) return null;

                byte networkErrorAsByte;
                var rttInMs = NetworkTransport.GetCurrentRTT(socketId.Value, ServerConnectionId.Value, out networkErrorAsByte);

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

        public void Start(object objectContainingRpcs, Func<object, UnityEngine.GameObject> createGameObjectFromState)
        {
            Assert.IsTrue(!IsStarted);

            ObjectContainingRpcs = objectContainingRpcs;
            this.createGameObjectFromState = createGameObjectFromState;
            ShouldApplyStateSnapshots = true;

            var maxConnectionCount = 1; // The client only connects to the server.
            var connectionConfig = NetLib.CreateConnectionConfig(
                out reliableSequencedChannelId,
                out reliableChannelId,
                out unreliableStateUpdateChannelId,
                out unreliableFragmentedChannelId,
                out unreliableChannelId
            );
            var hostTopology = new HostTopology(
                connectionConfig, maxConnectionCount
            );
            Start(null, hostTopology);
        }
        public override void Update()
        {
            base.Update();

            if (latestSequenceNumberDeltaGameStateBytesPair != null)
            {
                FinishHandlingDeltaGameState(
                    latestSequenceNumberDeltaGameStateBytesPair.Item1,
                    latestSequenceNumberDeltaGameStateBytesPair.Item2
                );
                latestSequenceNumberDeltaGameStateBytesPair = null;
            }
        }
        public override bool Stop()
        {
            var succeeded = base.Stop();

            if (!succeeded)
            {
                OsFps.Logger.LogError("Failed stopping client.");
            }

            ServerConnectionId = null;

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
                socketId.Value, ServerConnectionId.Value, out networkErrorAsByte
            );

            var networkError = (NetworkError)networkErrorAsByte;
            ServerConnectionId = null;

            return networkError;
        }

        public void SendMessageToServer(int channelId, byte[] messageBytes)
        {
            var networkError = SendMessage(ServerConnectionId.Value, channelId, messageBytes);
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

            ServerConnectionId = connectionId;

            if(OnConnectedToServer != null)
            {
                OnConnectedToServer();
            }
        }
        protected override void OnPeerDisconnected(int connectionId)
        {
            base.OnPeerConnected(connectionId);
            ServerConnectionId = null;

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

        private List<NetworkedGameState> cachedReceivedGameStates = new List<NetworkedGameState>();
        private System.Tuple<uint, byte[]> latestSequenceNumberDeltaGameStateBytesPair;
        private Func<object, UnityEngine.GameObject> createGameObjectFromState;

        private void OnReceiveDeltaGameStateFromServer(BinaryReader reader, byte[] bytesReceived, int numBytesReceived)
        {
            uint latestReceivedGameStateSequenceNumber = (cachedReceivedGameStates.Any())
                ? cachedReceivedGameStates[cachedReceivedGameStates.Count - 1].SequenceNumber
                : 0;
            latestReceivedGameStateSequenceNumber = System.Math.Max(
                latestReceivedGameStateSequenceNumber,
                latestSequenceNumberDeltaGameStateBytesPair?.Item1 ?? 0
            );

            var sequenceNumber = reader.ReadUInt32();
            if (sequenceNumber > latestReceivedGameStateSequenceNumber)
            {
                var bytesLeft = new byte[numBytesReceived - reader.BaseStream.Position];

                System.Array.Copy(bytesReceived, reader.BaseStream.Position, bytesLeft, 0, bytesLeft.Length);
                latestSequenceNumberDeltaGameStateBytesPair = new System.Tuple<uint, byte[]>(
                    sequenceNumber,
                    bytesLeft
                );
            }
        }

        private void FinishHandlingDeltaGameState(uint sequenceNumber, byte[] deltaBytes)
        {
            using (var memoryStream = new MemoryStream(deltaBytes))
            {
                using (var reader = new BinaryReader(memoryStream))
                {
                    var sequenceNumberRelativeTo = reader.ReadUInt32();
                    var networkedGameStateRelativeTo = GetNetworkedGameStateRelativeTo(sequenceNumberRelativeTo);

                    Profiler.BeginSample("State Deserialization");
                    var receivedGameState = NetworkSerializationUtils.DeserializeNetworkedGameState(
                        reader, sequenceNumber, networkedGameStateRelativeTo
                    );
                    Profiler.EndSample();

                    CallRpcOnServer("ServerOnReceiveClientGameStateAck", unreliableChannelId, new
                    {
                        gameStateSequenceNumber = receivedGameState.SequenceNumber
                    });

                    cachedReceivedGameStates.Add(receivedGameState);

                    var indexOfLatestGameStateToDiscard = cachedReceivedGameStates
                        .FindLastIndex(ngs => ngs.SequenceNumber < sequenceNumberRelativeTo);
                    if (indexOfLatestGameStateToDiscard >= 0)
                    {
                        var numberOfLatestGameStatesToDiscard = indexOfLatestGameStateToDiscard + 1;
                        cachedReceivedGameStates.RemoveRange(0, numberOfLatestGameStatesToDiscard);
                    }

                    Profiler.BeginSample("ClientOnReceiveGameState");
                    OnReceiveGameState(receivedGameState);
                    Profiler.EndSample();
                }
            }
        }
        private NetworkedGameState GetNetworkedGameStateRelativeTo(uint sequenceNumberRelativeTo)
        {
            var indexOfGameStateRelativeTo = cachedReceivedGameStates
                .FindIndex(ngs => ngs.SequenceNumber == sequenceNumberRelativeTo);

            Assert.IsTrue((indexOfGameStateRelativeTo >= 0) || (sequenceNumberRelativeTo == 0));

            return (indexOfGameStateRelativeTo >= 0)
                ? cachedReceivedGameStates[indexOfGameStateRelativeTo]
                : NetLib.GetEmptyNetworkedGameStateForDiffing();
        }

        private void OnReceiveGameState(NetworkedGameState receivedGameState)
        {
            if (!ShouldApplyStateSnapshots) return;

            if (OsFps.Instance.IsRemoteClient)
            {
                Profiler.BeginSample("Client Get Current Networked Game State");
                var oldComponentLists = NetLib.GetComponentStateListsToSynchronize(
                    receivedGameState.NetworkedComponentTypeInfos
                );
                Profiler.EndSample();

                Profiler.BeginSample("Client Apply Networked Game State");
                for (var i = 0; i < receivedGameState.NetworkedComponentStateLists.Count; i++)
                {
                    var componentList = receivedGameState.NetworkedComponentStateLists[i];
                    var networkedComponentTypeInfo = receivedGameState.NetworkedComponentTypeInfos[i];
                    var componentType = networkedComponentTypeInfo.StateType;
                    var oldComponentList = oldComponentLists[i];

                    NetLib.ApplyState(
                        networkedComponentTypeInfo, oldComponentList, componentList, createGameObjectFromState
                    );
                }
                Profiler.EndSample();
            }
        }

        private void OnReceiveDataFromServer(int channelId, byte[] bytesReceived, int numBytesReceived)
        {
            Profiler.BeginSample("OnReceiveDataFromServer");
            using (var memoryStream = new MemoryStream(bytesReceived, 0, numBytesReceived))
            {
                using (var reader = new BinaryReader(memoryStream))
                {
                    var messageTypeAsByte = reader.ReadByte();
                    RpcInfo rpcInfo;

                    if (messageTypeAsByte == NetLib.StateSynchronizationMessageId)
                    {
                        OnReceiveDeltaGameStateFromServer(reader, bytesReceived, numBytesReceived);
                    }
                    else if (NetLib.rpcInfoById.TryGetValue(messageTypeAsByte, out rpcInfo))
                    {
                        Profiler.BeginSample("Deserialize & Execute RPC");
                        var rpcArguments = NetworkSerializationUtils.DeserializeRpcCallArguments(rpcInfo, reader);
                        NetLib.ExecuteRpc(rpcInfo.Id, null, ObjectContainingRpcs, rpcArguments);
                        Profiler.EndSample();
                    }
                    else
                    {
                        throw new System.NotImplementedException("Unknown message type: " + messageTypeAsByte);
                    }
                }
            }
            Profiler.EndSample();
        }
    }
}