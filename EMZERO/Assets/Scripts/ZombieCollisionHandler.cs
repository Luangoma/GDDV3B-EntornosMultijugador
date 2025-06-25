using UnityEngine;

public class ZombieCollisionHandler : MonoBehaviour
{
    private void OnCollisionEnter(Collision collision)
    {
                PlayerController otherPlayer = collision.gameObject.GetComponent<PlayerController>();
        if (otherPlayer != null && !otherPlayer.isZombie) GameManager.Instance.TryConvertServerRpc(otherPlayer.NetworkObjectId);
    }
}


