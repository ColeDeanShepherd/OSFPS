using System;
using System.IO;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Networking;
using UnityEngine.Networking.Match;

namespace NetworkLibrary
{
    public class ServerPeer : NetworkPeer
    {
        public delegate void ClientConnectionChangeEventHandler(int connectionId);
        public event ClientConnectionChangeEventHandler OnClientConnected;
        public event ClientConnectionChangeEventHandler OnClientDisconnected;

        public delegate void ServerRegisteredWithMasterServerEventHandler(bool succeeded);
        public event ServerRegisteredWithMasterServerEventHandler OnRegisteredWithMasterServer;

        public bool ShouldSendStateSnapshots;
        public object ObjectContainingRpcs;

        private ThrottledAction sendGameStatePeriodicFunction;
        private int maxPlayerCount;

        public void Start(
            ushort? portNumber, int maxPlayerCount, object objectContainingRpcs, float sendGameStateInterval
        )
        {
            ObjectContainingRpcs = objectContainingRpcs;
            ShouldSendStateSnapshots = true;
            sendGameStatePeriodicFunction = new ThrottledAction(SendGameState, sendGameStateInterval);
            this.maxPlayerCount = maxPlayerCount;

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

            if (matchInfo != null)
            {
                StartUnregisteringWithMasterServer();
            }

            if (!succeeded)
            {
                OsFps.Logger.LogError("Failed stopping server.");
            }

            return succeeded;
        }

        public void LateUpdate()
        {
            if (ShouldSendStateSnapshots && (sendGameStatePeriodicFunction != null))
            {
                sendGameStatePeriodicFunction.TryToCall();
            }
        }
        
        public void StartRegisteringWithMasterServer(string serverName, string password, int requestDomain)
        {
            Assert.IsNull(matchInfo);

            NetLib.NetworkMatch.CreateMatch(
                matchName: serverName, matchSize: (uint)maxPlayerCount, matchAdvertise: true,
                matchPassword: password, publicClientAddress: "", privateClientAddress: "", eloScoreForMatch: 0,
                requestDomain: requestDomain, callback: InternalOnRegisteredWithMasterServer
            );
        }
        private void InternalOnRegisteredWithMasterServer(bool success, string extendedInfo, MatchInfo matchInfo)
        {
            if (success)
            {
                this.matchInfo = matchInfo;
                Utility.SetAccessTokenForNetwork(matchInfo.networkId, matchInfo.accessToken);

                StartConnectingToRelayServerAsServer();
            }
            else
            {
                OsFps.Logger.LogError("Failed creating a match. " + extendedInfo);
            }

            if (OnRegisteredWithMasterServer != null)
            {
                OnRegisteredWithMasterServer(success);
            }
        }

        private void StartUnregisteringWithMasterServer()
        {
            Assert.IsNotNull(matchInfo);

            NetLib.NetworkMatch.DestroyMatch(matchInfo.networkId, matchInfo.domain, OnUnregisteredWithMasterServer);
        }
        private void OnUnregisteredWithMasterServer(bool success, string extendedInfo)
        {
            if (!success)
            {
                OsFps.Logger.LogError("Failed unregistering a server. " + extendedInfo);
            }

            matchInfo = null;
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

            networkedGameStateCache.HandlePlayerDisconnect((uint)connectionId);

            if (OnClientDisconnected != null)
            {
                OnClientDisconnected(connectionId);
            }
        }
        protected override void OnReceiveData(int connectionId, int channelId, byte[] buffer, int numBytesReceived)
        {
            base.OnReceiveData(connectionId, channelId, buffer, numBytesReceived);
            OnReceiveDataFromClient(connectionId, channelId, buffer, numBytesReceived);
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

        private const int maxCachedSentGameStates = 100;
        private NetworkedGameStateCache networkedGameStateCache = new NetworkedGameStateCache(maxCachedSentGameStates);
        private void SendGameState()
        {
            // Get the current game state.
            var currentGameState = NetLib.GetCurrentNetworkedGameState(generateSequenceNumber: true);

            // Send the game state deltas.
            foreach (var connectionId in connectionIds)
            {
                var oldGameState = networkedGameStateCache.GetNetworkedGameStateToDiffAgainst((uint)connectionId);
                SendGameStateDiff(connectionId, currentGameState, oldGameState);
            }

            // Cache the game state for future deltas.
            networkedGameStateCache.AddGameState(currentGameState);
        }

        private void SendGameStateDiff(int connectionId, NetworkedGameState gameState, NetworkedGameState oldGameState)
        {
            byte[] messageBytes;

            using (var memoryStream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(memoryStream))
                {
                    writer.Write(NetLib.StateSynchronizationMessageId);
                    writer.Write(gameState.SequenceNumber);
                    writer.Write(oldGameState.SequenceNumber);
                    NetworkSerializationUtils.SerializeNetworkedGameState(writer, gameState, oldGameState);
                }

                messageBytes = memoryStream.ToArray();
            }
            
            SendMessageToClient(connectionId, unreliableFragmentedChannelId, messageBytes);
        }

        [Rpc(ExecuteOn = NetworkPeerType.Server)]
        private void ServerOnReceiveClientGameStateAck(uint gameStateSequenceNumber)
        {
            networkedGameStateCache.AcknowledgeGameStateForPlayer(
                (uint)CurrentRpcSenderConnectionId, gameStateSequenceNumber
            );
        }

        public int CurrentRpcSenderConnectionId;
        private void OnReceiveDataFromClient(int connectionId, int channelId, byte[] bytesReceived, int numBytesReceived)
        {
            using (var memoryStream = new MemoryStream(bytesReceived, 0, numBytesReceived))
            {
                using (var reader = new BinaryReader(memoryStream))
                {
                    var messageTypeAsByte = reader.ReadByte();

                    RpcInfo rpcInfo;

                    if (messageTypeAsByte == NetLib.StateSynchronizationMessageId)
                    {
                        throw new System.NotImplementedException("Servers don't support receiving state synchronization messages.");
                    }
                    else if (NetLib.rpcInfoById.TryGetValue(messageTypeAsByte, out rpcInfo))
                    {
                        var rpcArguments = NetworkSerializationUtils.DeserializeRpcCallArguments(rpcInfo, reader);

                        CurrentRpcSenderConnectionId = connectionId;

                        if (rpcInfo.Name != "ServerOnReceiveClientGameStateAck")
                        {
                            NetLib.ExecuteRpc(rpcInfo.Id, ObjectContainingRpcs, null, rpcArguments);
                        }
                        else
                        {
                            NetLib.ExecuteRpc(rpcInfo.Id, this, null, rpcArguments);
                        }
                    }
                    else
                    {
                        throw new System.NotImplementedException("Unknown message type: " + messageTypeAsByte);
                    }
                }
            }
        }
    }
}