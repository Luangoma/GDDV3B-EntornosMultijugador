using Unity.Netcode;
using UnityEngine;

// Se ha comentado todo lo que ha sido movido a player controller -----------------------------------------
// Esta clase pasa a ser �nicamente informativa (ser� eliminada en la entrega) ----------------------------
public class PlayerSpawn : NetworkBehaviour
{
    public void MoveRandom()
    {
        SubmitPositionRandomRequestRpc();
    }

    private static Vector3 GetRandomPositionOnPlane()
    {
        return new Vector3(Random.Range(-3f, 3f), 1f, Random.Range(-3f, 3f));
    }

    [Rpc(SendTo.Server)]
    private void SubmitPositionRandomRequestRpc(RpcParams rpcParams = default)
    {
        var randomPosition = GetRandomPositionOnPlane();
        transform.position = randomPosition;
    }
}