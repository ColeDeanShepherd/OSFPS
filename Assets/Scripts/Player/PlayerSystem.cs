using Unity.Entities;
using System.Collections.Generic;

public class PlayerSystem : ComponentSystem
{
    public struct Data
    {
        public PlayerComponent PlayerComponent;
    }

    public static PlayerSystem Instance;

    public PlayerSystem()
    {
        Instance = this;
    }

    protected override void OnUpdate()
    {
        var server = OsFps.Instance?.Server;
        if (server != null)
        {
            ServerOnUpdate(server);
        }
    }

    private void ServerOnUpdate(Server server)
    {
        foreach (var entity in GetEntities<Data>())
        {
            var playerState = entity.PlayerComponent.State;
            var playerId = playerState.Id;
            var connectionId = server.GetConnectionIdByPlayerId(playerId);
            var rttInMs = (connectionId != null)
                ? server.ServerPeer.GetRoundTripTimeToClientInMilliseconds(connectionId.Value)
                : 0;
            playerState.RoundTripTimeInMilliseconds = (rttInMs != null)
                ? (ushort)(rttInMs.Value)
                : (ushort)0;
        }
    }
}