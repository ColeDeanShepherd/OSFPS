using System.Collections.Generic;
using System.Linq;

namespace NetworkLibrary
{
    public class NetworkedGameStateCache
    {
        public NetworkedGameStateCache(int maxCachedSentGameStates)
        {
            this.maxCachedSentGameStates = maxCachedSentGameStates;
        }
        public void AddGameState(NetworkedGameState gameState)
        {
            cachedSentGameStates.Add(gameState);
            if (cachedSentGameStates.Count > maxCachedSentGameStates)
            {
                cachedSentGameStates.RemoveAt(0);
            }
        }
        public void AcknowledgeGameStateForPlayer(uint playerId, uint gameStateSequenceNumber)
        {
            var playersLatestAcknowledgedSequenceNumber =
            latestAcknowledgedGameStateSequenceNumberByPlayerId.GetValueOrDefault(playerId);

            if (gameStateSequenceNumber > playersLatestAcknowledgedSequenceNumber)
            {
                latestAcknowledgedGameStateSequenceNumberByPlayerId[playerId] = gameStateSequenceNumber;
            }

            RemoveUnneededGameStates();
        }
        public void OnPlayerDisconnected(uint playerId)
        {
            latestAcknowledgedGameStateSequenceNumberByPlayerId.Remove(playerId);
            RemoveUnneededGameStates();
        }
        public NetworkedGameState GetNetworkedGameStateToDiffAgainst(uint playerId)
        {
            var playersLatestAcknowledgedGameStateSequenceNumber =
                latestAcknowledgedGameStateSequenceNumberByPlayerId.GetValueOrDefault(playerId);
            var indexOfPlayersLatestAcknowledgedGameState = cachedSentGameStates
                .FindIndex(ngs => ngs.SequenceNumber == playersLatestAcknowledgedGameStateSequenceNumber);
            var playersLatestAcknowledgedGameState = (indexOfPlayersLatestAcknowledgedGameState >= 0)
                ? cachedSentGameStates[indexOfPlayersLatestAcknowledgedGameState]
                : NetLib.GetEmptyNetworkedGameStateForDiffing();

            return playersLatestAcknowledgedGameState;
        }

        private int maxCachedSentGameStates;
        private List<NetworkedGameState> cachedSentGameStates = new List<NetworkedGameState>();
        private Dictionary<uint, uint> latestAcknowledgedGameStateSequenceNumberByPlayerId = new Dictionary<uint, uint>();

        private void RemoveUnneededGameStates()
        {
            if (latestAcknowledgedGameStateSequenceNumberByPlayerId.Count == 0)
            {
                cachedSentGameStates.Clear();
            }
            else
            {
                var minLatestAcknowledgedGameStateSequenceNumber = latestAcknowledgedGameStateSequenceNumberByPlayerId
                    .Select(kvp => kvp.Value)
                    .Min();
                cachedSentGameStates = cachedSentGameStates
                    .Where(ngs => ngs.SequenceNumber >= minLatestAcknowledgedGameStateSequenceNumber)
                    .ToList();
            }
        }
    }
}