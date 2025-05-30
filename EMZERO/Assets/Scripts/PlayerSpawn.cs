using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

public class PlayerSpawn : NetworkBehaviour
{
    private NetworkVariable<Vector3> Position = new NetworkVariable<Vector3>();

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            spawnFirstTime();
        }
    }

    // Update is called once per frame
    void Update()
    {
        // Movimiento random 
        //transform.position = Position.Value;
    }
    public void MoveRandom()
    {
        SubmitPositionRandomRequestRpc();
    }
    public void spawnFirstTime() {
        SubmitPositionRequestRpc();
    }
    static Vector3 GetRandomPositionOnPlane()
    {
        return new Vector3(UnityEngine.Random.Range(-3f, 3f), 1f, UnityEngine.Random.Range(-3f, 3f));
    }

    [Rpc(SendTo.Server)]
    private void SubmitPositionRandomRequestRpc(RpcParams rpcParams = default)
    {
        var randomPosition = GetRandomPositionOnPlane();
        transform.position = randomPosition;
        Position.Value = randomPosition;
    }
    
    [Rpc(SendTo.Server)]
    private void SubmitPositionRequestRpc(RpcParams rpcParams = default)
    {
        var startPoint = new UnityEngine.Vector3(3f,1f,4f);
        transform.position = new UnityEngine.Vector3(3f, 1f, 4f);
        Position.Value = new UnityEngine.Vector3(3f, 1f, 4f);
    }
}
