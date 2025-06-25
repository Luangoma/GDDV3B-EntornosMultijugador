using Unity.Netcode;
using UnityEngine;

public class RpcTest : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        if (!IsServer && IsOwner) //Only send an RPC to the server from the client that owns the NetworkObject of this NetworkBehaviour instance
        {
            ServerOnlyRpc(0, NetworkObjectId);
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ClientAndHostRpc(int value, ulong sourceNetworkObjectId)
    {
                if (IsOwner) //Only send an RPC to the owner of the NetworkObject
        {
            ServerOnlyRpc(value + 1, sourceNetworkObjectId);
        }
    }

    [Rpc(SendTo.Server)]
    private void ServerOnlyRpc(int value, ulong sourceNetworkObjectId)
    {
                ClientAndHostRpc(value, sourceNetworkObjectId);
    }
}