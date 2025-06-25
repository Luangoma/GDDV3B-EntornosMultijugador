using UnityEngine;

public class ZombieCollisionHandler : MonoBehaviour
{
    /*private void OnCollisionEnter(Collision collision)
    {
        PlayerController playerController = collision.gameObject.GetComponent<PlayerController>();
                if (playerController != null && !playerController.isZombie)
        {
            playerController.isZombie = true;
            
            // Obtener el prefab de humano desde el LevelManager
            LevelManager levelManager = FindObjectOfType<LevelManager>();
            if (levelManager != null && collision.gameObject.name.Contains(levelManager.PlayerPrefabName))
            {
                // Cambiar el humano a zombie
                levelManager.ChangeToZombie(collision.gameObject, playerController.enabled);
            }
        }
    }*/

    private void OnCollisionEnter(Collision collision)
    {
                PlayerController otherPlayer = collision.gameObject.GetComponent<PlayerController>();
        if (otherPlayer != null && !otherPlayer.isZombie) GameManager.Instance.TryConvertServerRpc(otherPlayer.NetworkObjectId);
    }
}


