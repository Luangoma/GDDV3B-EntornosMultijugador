using System;
using System.Xml.Serialization;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

public class PlayerController : NetworkBehaviour
{


    System.Random rand = new System.Random();
    [SerializeField] private GameObject playerCameraPrefab;
    [SerializeField] private GameObject playerNameUIPrefab;
    private TextMeshProUGUI playerNameText;
    private GameObject playerNameUIInstance;
    [Header("Stats")]

    [Header("Character settings")]
    public bool isZombie = false; // A adir una propiedad para el estado del jugador
    public string uniqueID; // A adir una propiedad para el identificador  nico
    public NetworkVariable<bool> WasOriginallyZombie = new NetworkVariable<bool>(false);
    public bool convertido;

    [Header("Movement Settings")]
    public float moveSpeed = 5f;           // Velocidad de movimiento
    public float zombieSpeedModifier = 0.8f; // Modificador de velocidad para zombies
    public Animator animator;                   // Referencia al Animator
    public Transform cameraTransform;      // Referencia a la c mara

    private float horizontalInput;         // Entrada horizontal (A/D o flechas)
    private float verticalInput;           // Entrada vertical (W/S o flechas)

    private TextMeshProUGUI coinText;
    private TextMeshProUGUI timeText;

    #region Variables compartidas/game manager
    private NetworkVariable<Quaternion> Rotation = new NetworkVariable<Quaternion>();
    private NetworkVariable<Vector3> Position = new NetworkVariable<Vector3>();
    public NetworkVariable<FixedString64Bytes> playerName = new NetworkVariable<FixedString64Bytes>(
    default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    GameManager gameManager;
    GameObject camObj;
    #endregion




    public override void OnNetworkSpawn()
    {
        gameManager = GameManager.Instance;
        if (IsOwner)
        {
            WasOriginallyZombie.Value = isZombie;
            Debug.Log($"Player {OwnerClientId} spawned - isZombie: {isZombie}, WasOriginallyZombie: {WasOriginallyZombie.Value}");


            // Asigna la c mara principal a este jugador local
            //Camera mainCamera = Camera.main;
            //if (mainCamera != null)
            //{
            //    CameraController cameraController = mainCamera.GetComponent<CameraController>();
            //    if (cameraController != null)
            //    {
            //        cameraController.player = this.transform;
            //    }
            //}
            // Instancia una c mara solo para este jugador
            Camera mainCam = Camera.main;
            Camera cam = null;
            if (mainCam != null)
            {
                cameraTransform = mainCam.transform;
                camObj = mainCam.gameObject;

                // Asigna el jugador al CameraController de esa c mara
                CameraController cameraController = camObj.GetComponent<CameraController>();
                if (cameraController != null)
                {
                    Debug.Log("C mara existente asignada al nuevo jugador local");
                    cameraController.player = this.transform;
                }

            }
            else
            {
                if (playerCameraPrefab == null)
                {
                    Debug.LogWarning("playerCameraPrefab no asignado en PlayerController.");
                    return;
                }
                camObj = Instantiate(playerCameraPrefab);
                cam = camObj.GetComponent<Camera>();
                cam.tag = "MainCamera";
                cameraTransform = cam.transform;

                // Asigna el jugador al CameraController de esa c mara
                CameraController cameraController = camObj.GetComponent<CameraController>();
                if (cameraController != null)
                {
                    Debug.Log("C mara asignada al jugador local");
                    cameraController.player = this.transform;
                }
                // Generar nombre nuevo solo en el caso de que la camara tambien sea nueva
            }
            Canvas[] canvases = camObj.GetComponentsInChildren<Canvas>(true);
            foreach (var canvas in canvases)
            {
                if (canvas.renderMode == RenderMode.WorldSpace && cam != null)
                {
                    canvas.worldCamera = cam;
                }
            }
            // Utiliza la lista en Gamemanager para obtener el nombre de cada player
            SetPlayerNameServerRpc();
        }
        else
        {
            Position.OnValueChanged += OnPositionChanged;
            Rotation.OnValueChanged += OnRotationChanged;
        }

        // Instanciar el UI del nombre
        if (playerNameUIPrefab != null)
        {
            playerNameUIInstance = Instantiate(playerNameUIPrefab, transform);
            playerNameUIInstance.transform.localPosition = new Vector3(0, 0.7f, 0); // Ajustar altura
            playerNameText = playerNameUIInstance.GetComponentInChildren<TextMeshProUGUI>();
            playerNameText.text = playerName.Value.ToString();
        }

        gameManager.collectedCoins.OnValueChanged += OnCoinsIncreased;
        //gameManager.timeRemaining.OnValueChanged += OnTimeChanged;
        playerName.OnValueChanged += OnPlayerNameChanged;
        CanvasStart();
    }

    // Esto seguramente de valores incorrectos si varios cogen a la vez (creo)
    private void OnCoinsIncreased(int previousValue, int newValue)
    {
        UpdateCoinUI(newValue);
    }
    
    // Esto seguramente de valores incorrectos si varios cogen a la vez (creo)
    /*private void OnTimeChanged(float previousValue, float newValue)
    {
        UpdateTimeUI(newValue);
    }*/

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

    [ServerRpc]
    private void SetPlayerNameServerRpc(ServerRpcParams rpcParams = default)
    {
        //if (gameManager.backupPlayerNames[rpcParams.Receive.SenderClientId].Length > 0)
        //{
        //    Debug.LogError("Este jugador ya tenía nombre");
        //    Debug.Log($"Player last name: {playerName.Value}");
        //    return;
        //}
        playerName.Value = gameManager.backupPlayerNames[rpcParams.Receive.SenderClientId];
        Debug.Log($"Player name set to: {playerName.Value}");
    }

    private void OnPlayerNameChanged(FixedString64Bytes oldValue, FixedString64Bytes newValue)
    {
        if (playerNameText != null)
            playerNameText.text = newValue.ToString();
    }

    void Start()
    {
        // -----------------------------------------------------------------------------------------
        // Todo esto solo funciona en la escena de la partida, porque el canvas no existe en el menu
        // -----------------------------------------------------------------------------------------
        // Buscar el objeto "CanvasPlayer" en la escena
        // CanvasStart(); // Lo he movido al propio onNetworkSpawn porque creo que puede evitar errores de orden de ejecucion pero aqui tambien funciona

    }

    private void CanvasStart()
    {
        if (!IsOwner)
        {
            // Si no eres owner, no tienes camara
            cameraTransform = null;
            return;
        }
        GameObject canvas = GameObject.Find("CanvasPlayer");

        if (canvas != null)
        {

            cameraTransform = Camera.main.transform;
            Transform panel = canvas.transform.Find("PanelHud");

            if (panel != null)
            {
                // Buscar el TextMeshProUGUI llamado "CoinsValue" dentro del Panel
                Transform coinTextTransform = panel.Find("CoinsValue");
                Transform timeTextTransform = panel.Find("TimeValue");

                if (coinTextTransform != null)
                {
                    coinText = coinTextTransform.GetComponent<TextMeshProUGUI>();
                }
                if (timeTextTransform != null)
                {
                    timeText = timeTextTransform.GetComponent<TextMeshProUGUI>();
                }
            }
            UpdateCoinUI(gameManager.collectedCoins.Value);
            //UpdateTimeUI(gameManager.timeRemaining.Value);
        }
    }
    private void LateUpdate()
    {
        // Importante, este codigo no requiere de if (IsOwner) porque se ejecuta en todos los clientes,
        // para que el texto de cada cliente mire a la camara de cada cliente y se vea recto en su cliente
        // Hacer que el nombre mire siempre a la cámara
        if (playerNameUIInstance != null && Camera.main != null)
        {
            playerNameUIInstance.transform.LookAt(Camera.main.transform);
            playerNameUIInstance.transform.Rotate(0, 180, 0); // Para que no salga al revés
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
        // Calcular la direcci n de movimiento en relaci n a la c mara
        Vector3 moveDirection = (cameraTransform.forward * verticalInput + cameraTransform.right * horizontalInput).normalized;
        moveDirection.y = 0f; // Asegurarnos de que el movimiento es horizontal (sin componente Y)

        // Mover el jugador usando el Transform
        if (moveDirection != Vector3.zero)
        {
            //Debug.Log("Puedo controlar a este jugador !!!!!!");

            // Calcular la rotaci n en Y basada en la direcci n del movimiento
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
            SubmitRotationServerRpc(targetRotation);

            // Ajustar la velocidad si es zombie
            float adjustedSpeed = isZombie ? moveSpeed * zombieSpeedModifier : moveSpeed;

            // Mover al jugador en la direcci n deseada
            //transform.Translate(moveDirection * adjustedSpeed * Time.deltaTime, Space.World);
            SubmitMovementServerRpc(moveDirection * adjustedSpeed * Time.deltaTime);
        }
    }
    private void OnDestroy()
    {
        Rotation.OnValueChanged -= OnRotationChanged;
        Position.OnValueChanged -= OnPositionChanged;
        gameManager.collectedCoins.OnValueChanged -= OnCoinsIncreased;
        if (gameManager != null) {
            if (isZombie)
            {
                gameManager.zombieNumber.Value--;
                gameManager.ZombiesDesconectados.Value++;
            }
            else
            {
                gameManager.humanNumber.Value--;
                if (!convertido)
                {
                    gameManager.HumanosDesconectados.Value++;
                }
            }
        gameManager.NotifyPlayerTransformedServerRpc();
        }
    }


    void HandleAnimations()
    {
        // Animaciones basadas en la direcci n del movimiento
        animator.SetFloat("Speed", Mathf.Abs(horizontalInput) + Mathf.Abs(verticalInput));  // Controla el movimiento (caminar/correr)
    }

    public void CoinCollected()
    {
        if (!IsServer) return;
        if (!isZombie) // Solo los humanos pueden recoger monedas
        {
            //gameManager.collectedCoins.Value++;
            UpdateCoinUI(gameManager.collectedCoins.Value);

            Debug.Log($"Jugador {OwnerClientId} recolectó moneda (local)");
            RequestCoinCollectionServerRpc();
            Debug.Log($"Se envió petición al servidor desde {OwnerClientId}");
        }
    }


    [ServerRpc (RequireOwnership = false)]  // Cualquier humano puede solicitar recolectar monedas
    private void RequestCoinCollectionServerRpc()
    {
        // Solo el servidor puede modificar este valor
        //gameManager.collectedCoins.Value++;
        Debug.Log($"Monedas recolectadas (local): {gameManager.collectedCoins.Value}");

        // Notificar al GameManager
        gameManager.NotifyCoinCollectedServerRpc();
        //GameManager.Instance?.NotifyCoinCollectedServerRpc();
    }

    // Llamado cuando la variable coins ha sido modificada en el servidor
    public void UpdateCoinUI(int coinCount)
    {
        if (coinText != null)
        {
            coinText.text = $"{coinCount}";
        }
    }

    /*public void UpdateTimeUI(float timeCount) {
        if (timeText != null)
        {
            // Convertir a minutos y segundos
            int minutes = Mathf.FloorToInt(timeCount / 60f);
            int seconds = Mathf.FloorToInt(timeCount % 60f);
            timeText.text = $"{minutes:00}:{seconds:00}";
        }
    }*/


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

