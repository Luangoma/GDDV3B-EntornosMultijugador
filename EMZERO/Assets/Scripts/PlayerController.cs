using System;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

public class PlayerController : NetworkBehaviour
{
    private TextMeshProUGUI coinText;
    private NetworkManager p_NetworkManager;

    [Header("Stats")]
    // Ahora es variable compartida -- En un foro vi cosas de permisos como NetworkVariableReading.everyone o algo asi
    public NetworkVariable<int> CoinsCollected =new NetworkVariable<int> (0);

    [Header("Character settings")]
    public bool isZombie = false; // Añadir una propiedad para el estado del jugador
    public string uniqueID; // Añadir una propiedad para el identificador único

    [Header("Movement Settings")]
    public float moveSpeed = 5f;           // Velocidad de movimiento
    public float zombieSpeedModifier = 0.8f; // Modificador de velocidad para zombies
    public Animator animator;              // Referencia al Animator
    public Transform cameraTransform;      // Referencia a la cámara

    private float horizontalInput;         // Entrada horizontal (A/D o flechas)
    private float verticalInput;           // Entrada vertical (W/S o flechas)


    // Metodo cutre preliminar que asigna el contolador del host a uno de los jugadores ya instanciados
    public override void OnNetworkSpawn()
    {
        if (IsHost)
        {
            PlayerController[] players = FindObjectsOfType<PlayerController>();

            foreach (var player in players)
            {
                if (!player.NetworkObject.IsOwnedByServer)
                {
                    // El NetworkManager.Singleton creo que va a haber que usarle mucho, porque es el equivalente al m_NetworkManager del NM_script
                    player.NetworkObject.ChangeOwnership(NetworkManager.Singleton.LocalClientId);
                    player.isZombie = false;
                    break;
                }
                else { 
                    // Crear un nuevo player jugable
                    
                }
            }
        }
    }

    void Start()
    {
        //Si no eres owner vemos si tienes una camara activa y la quitamos
        //if (!IsOwner)
        //{
        //    if (cameraTransform != null)
        //    {
        //        cameraTransform.gameObject.SetActive(false);
        //    }
        //    return;
        //}
        if (!IsClient && !IsServer)
        {
            cameraTransform = Camera.main.transform;
        }


        // Buscar el objeto "CanvasPlayer" en la escena
        GameObject canvas = GameObject.Find("CanvasPlayer");

        if (canvas != null)
        {
            Debug.Log("Canvas encontrado");

            // Buscar el Panel dentro del CanvasHud
            Transform panel = canvas.transform.Find("PanelHud");
            if (panel != null)
            {
                // Buscar el TextMeshProUGUI llamado "CoinsValue" dentro del Panel
                Transform coinTextTransform = panel.Find("CoinsValue");
                if (coinTextTransform != null)
                {
                    coinText = coinTextTransform.GetComponent<TextMeshProUGUI>();
                }
            }
        }

        UpdateCoinUI();
    }

    void Update()
    {
        // El jugador solamente controla su personaje
        if (!IsOwner)
        {
            return;
        }
        //Debug.Log("NO me esta dejando controlar a este jugador !!!!!!");
        Debug.Log("puedo moverme");
        // Leer entrada del teclado
        horizontalInput = Input.GetAxis("Horizontal");
        verticalInput = Input.GetAxis("Vertical");

        // Mover el jugador
        MovePlayer();

        // Manejar las animaciones del jugador
        HandleAnimations();
    }
    void MovePlayer()
    {
        Debug.Log("1");
        if (cameraTransform == null) { return; }
        Debug.Log("2");
        // Calcular la dirección de movimiento en relación a la cámara
        Vector3 moveDirection = (cameraTransform.forward * verticalInput + cameraTransform.right * horizontalInput).normalized;
        moveDirection.y = 0f; // Asegurarnos de que el movimiento es horizontal (sin componente Y)

        // Mover el jugador usando el Transform
        if (moveDirection != Vector3.zero)
        {
            Debug.Log("Puedo controlar a este jugador !!!!!!");
            // Calcular la rotación en Y basada en la dirección del movimiento
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, 720f * Time.deltaTime);

            // Ajustar la velocidad si es zombie
            float adjustedSpeed = isZombie ? moveSpeed * zombieSpeedModifier : moveSpeed;

            // Mover al jugador en la dirección deseada
            //transform.Translate(moveDirection * adjustedSpeed * Time.deltaTime, Space.World);
            if (moveDirection.magnitude > 0.01f)
            {
                SubmitMovementServerRpc(moveDirection * adjustedSpeed * Time.deltaTime);
            }
        }
    }

    void HandleAnimations()
    {
        // Animaciones basadas en la dirección del movimiento
        animator.SetFloat("Speed", Mathf.Abs(horizontalInput) + Mathf.Abs(verticalInput));  // Controla el movimiento (caminar/correr)
    }

    public void CoinCollected()
    {
        if (!isZombie) // Solo los humanos pueden recoger monedas
        {
            CoinsCollected.Value++;
            UpdateCoinUI();
        }
    }

    void UpdateCoinUI()
    {
        if (coinText != null)
        {
            coinText.text = $"{CoinsCollected.Value}";
        }
    }

    public void spawnFirstTime()
    {
        SubmitPositionRequestRpc();
    }

    [Rpc(SendTo.Server)]
    private void SubmitPositionRequestRpc(RpcParams rpcParams = default)
    {
        var startPoint = new UnityEngine.Vector3(3f, 1f, 4f);
        transform.position = new UnityEngine.Vector3(3f, 1f, 4f);
        //Position.Value = new UnityEngine.Vector3(3f, 1f, 4f);
    }
    // Metodo para que el servidor reciba que se ha cogido una moneda adicional
    [ServerRpc]
    public void coinCollectedServerRpc() // Este suma el valor y les dice a todos los usuarios que se ha sumado una moneda
    {
        CoinsCollected.Value++;
        UpdateCoinOnlineClientRpc(CoinsCollected.Value);
        UpdateCoinUI();
    }

    // Metodo que se llamara cuando el servidor mande un mensajito
    // Transferir la nueva data de las monedas a todas las interfaces
    // Porque en teoria las interfaces no son comunes
    [ClientRpc]
    void UpdateCoinOnlineClientRpc(int coins)
    {
        CoinsCollected.Value = coins;
        UpdateCoinUI();
    }

    // Actualizar del movimiento del personaje al servidor en tiempo real
    [ServerRpc]
    private void SubmitMovementServerRpc(Vector3 moveVector)
    {
        transform.position += moveVector;
    }



    /// <summary>
    /// Temporal para pruebas ! ! ! ! ! !  ! ! ! ! ! ! !  ! ! ! !  ! 
    /// </summary>
    public void MoveRandom()
    {
        SubmitPositionRandomRequestRpc();
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
        //Position.Value = randomPosition;
    }

}

