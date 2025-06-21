using System;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

public class PlayerController : NetworkBehaviour
{
    private TextMeshProUGUI coinText;
    System.Random rand = new System.Random();
    [SerializeField] private GameObject playerCameraPrefab;
    [Header("Stats")]
    // Ahora es variable compartida -- En un foro vi cosas de permisos como NetworkVariableReading.everyone o algo asi
    public NetworkVariable<int> CoinsCollected = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("Character settings")]
    public bool isZombie = false; // Añadir una propiedad para el estado del jugador
    public string uniqueID; // Añadir una propiedad para el identificador único

    [Header("Movement Settings")]
    public float moveSpeed = 5f;           // Velocidad de movimiento
    public float zombieSpeedModifier = 0.8f; // Modificador de velocidad para zombies
    public Animator animator;                   // Referencia al Animator
    public Transform cameraTransform;      // Referencia a la cámara

    private float horizontalInput;         // Entrada horizontal (A/D o flechas)
    private float verticalInput;           // Entrada vertical (W/S o flechas)

    private NetworkVariable<Quaternion> Rotation = new NetworkVariable<Quaternion>();

    private NetworkVariable<Vector3> Position = new NetworkVariable<Vector3>();

    private GameManager gm;

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            // Asigna la cámara principal a este jugador local
            //Camera mainCamera = Camera.main;
            //if (mainCamera != null)
            //{
            //    CameraController cameraController = mainCamera.GetComponent<CameraController>();
            //    if (cameraController != null)
            //    {
            //        cameraController.player = this.transform;
            //    }
            //}
            // Instancia una cámara solo para este jugador
            if (playerCameraPrefab == null)
            {
                Debug.LogWarning("playerCameraPrefab no asignado en PlayerController.");
                return;
            }
            GameObject camObj = Instantiate(playerCameraPrefab);
            Camera cam = camObj.GetComponent<Camera>();
            cam.tag = "MainCamera"; // Opcional, si quieres usar Camera.main
            cameraTransform = cam.transform;

            // Asigna el jugador al CameraController de esa cámara
            CameraController cameraController = camObj.GetComponent<CameraController>();
            if (cameraController != null)
            {
                Debug.Log("Cámara asignada al jugador local");
                cameraController.player = this.transform;
            }

        }
        else
        {
            Position.OnValueChanged += OnPositionChanged;
            Rotation.OnValueChanged += OnRotationChanged;
        }

        CoinsCollected.OnValueChanged += OnCoinsIncreased;
    }

    // Esto seguramente de valores incorrectos si varios cogen a la vez (creo)
    private void OnCoinsIncreased(int previousValue, int newValue)
    {
        CoinsCollected.Value = newValue;
        UpdateCoinUI();
    }

    // Este metodo se dispara para cada cliente en el momento de que la networkVariable Position cambie
    private void OnPositionChanged(Vector3 oldPos, Vector3 newPos)
    {
        transform.position = newPos;
    }
    // Este metodo se dispara para cada cliente en el momento de que la networkVariable Rotation cambie
    private void OnRotationChanged(Quaternion oldRot, Quaternion newRot)
    {
        transform.rotation = newRot;
    }
    

    // Metodo cutre preliminar que asigna el contolador del host a uno de los jugadores ya instanciados
    //public override void OnNetworkSpawn()
    //{

    //    if (IsHost)
    //    {
    //        Debug.Log("Host conectado");

    //    }
    //    else if (IsClient) {

    //    }

    //}

    void Start()
    {
        //Si no eres owner vemos si tienes una camara activa y la quitamos

        // -----------------------------------------------------------------------------------------
        // Todo esto solo funciona en la escena de la partida, porque el canvas no existe en el menu
        // -----------------------------------------------------------------------------------------
        // Buscar el objeto "CanvasPlayer" en la escena

        gm = FindObjectOfType<GameManager>();

        GameObject canvas = GameObject.Find("CanvasPlayer");

        if (canvas != null)
        {
            if (IsOwner)
            {
                cameraTransform = Camera.main.transform;
                // Instanciar una nueva cámara solo para este cliente
                //GameObject cameraObj = Instantiate(cameraPrefab);
                //cameraTransform = cameraObj.GetComponent<Camera>().transform;
            }
            else
            {
                // Si no eres owner, no tienes camara
                cameraTransform = null;
            }
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
            UpdateCoinUI();
        }

    }

    void Update()
    {
        // El jugador solamente controla su personaje
        if (!IsOwner)
        {
            return;
        }
        //Debug.Log("NO me esta dejando controlar a este jugador !!!!!!");
        //Debug.Log("puedo moverme");
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
        //Debug.Log("1");
        if (cameraTransform == null) { return; }
        //Debug.Log("2");
        // Calcular la dirección de movimiento en relación a la cámara
        Vector3 moveDirection = (cameraTransform.forward * verticalInput + cameraTransform.right * horizontalInput).normalized;
        moveDirection.y = 0f; // Asegurarnos de que el movimiento es horizontal (sin componente Y)

        // Mover el jugador usando el Transform
        if (moveDirection != Vector3.zero)
        {
            //Debug.Log("Puedo controlar a este jugador !!!!!!");

            // Calcular la rotación en Y basada en la dirección del movimiento
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
            SubmitRotationServerRpc(targetRotation);

            // Ajustar la velocidad si es zombie
            float adjustedSpeed = isZombie ? moveSpeed * zombieSpeedModifier : moveSpeed;

            // Mover al jugador en la dirección deseada
            //transform.Translate(moveDirection * adjustedSpeed * Time.deltaTime, Space.World);
            SubmitMovementServerRpc(moveDirection * adjustedSpeed * Time.deltaTime);
        }
    }
    private void OnDestroy()
    {
        Rotation.OnValueChanged -= OnRotationChanged;        
        Position.OnValueChanged -= OnPositionChanged;
        
    }
    

    void HandleAnimations()
    {
        // Animaciones basadas en la dirección del movimiento
        animator.SetFloat("Speed", Mathf.Abs(horizontalInput) + Mathf.Abs(verticalInput));  // Controla el movimiento (caminar/correr)
    }

    public void CoinCollected()
    {
        if (!isZombie && IsOwner)
        {
            Debug.Log($"Moneda recolectada por humano. Actual: {CoinsCollected.Value}");
            RequestCoinCollectionServerRpc();
        }
    }

    [ServerRpc]
    private void RequestCoinCollectionServerRpc()
    {
        // Incrementar las monedas del jugador
        CoinsCollected.Value++;

        // Notificar al GameManager
        GameManager.Instance.NotifyCoinCollectedServerRpc();

        Debug.Log($"Monedas totales: {CoinsCollected.Value}");
    }

    void UpdateCoinUI()
    {
        if (coinText != null)
        {
            coinText.text = $"{CoinsCollected.Value}";
        }
    }

    // Metodo para que el servidor reciba que se ha cogido una moneda adicional
    [ServerRpc]
    public void coinCollectedServerRpc() // Este suma el valor y les dice a todos los usuarios que se ha sumado una moneda
    {
        CoinsCollected.Value++;
        //UpdateCoinOnlineClientRpc(CoinsCollected.Value);
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
        Position.Value = transform.position;
    }

    [ServerRpc]
    private void SubmitRotationServerRpc(Quaternion quaternion)
    {
        transform.rotation = quaternion;
        Rotation.Value = quaternion;
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

