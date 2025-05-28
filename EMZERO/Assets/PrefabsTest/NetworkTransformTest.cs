using Unity.Netcode;
using UnityEngine;

public class NetworkTransformTest : NetworkBehaviour
{
    public float moveSpeed = 10f;

    private void Update()
    {
        if (!IsOwner) return;

        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");

        Vector3 direction = new Vector3(moveX, 0f, moveZ);

        if (direction.magnitude > 0.01f)
        {
            // Enviás el movimiento al servidor (con la dirección y velocidad)
            SubmitMovementServerRpc(direction.normalized);
        }
    }

    [ServerRpc]
    private void SubmitMovementServerRpc(Vector3 direction)
    {
        transform.position += direction * moveSpeed * Time.deltaTime;
    }
}
