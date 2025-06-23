using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum GameMode
{
    Tiempo,
    Monedas
}

public class LevelManager : MonoBehaviour
{
    #region Properties

    [Header("Prefabs")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private GameObject zombiePrefab;

    [Header("Team Settings")]

    [Header("Game Mode Settings")]
    [Tooltip("Selecciona el modo de juego")]
    [SerializeField] private GameMode gameMode;

    [Tooltip("Tiempo de partida en minutos para el modo tiempo")]
    [SerializeField] private int minutes = 5;

    private List<Vector3> SpawnPoints = new List<Vector3>();

    private TextMeshProUGUI humansText;
    private TextMeshProUGUI zombiesText;
    private TextMeshProUGUI gameModeText;
    private TextMeshProUGUI timeText;

    public string PlayerPrefabName => playerPrefab.name;
    public string ZombiePrefabName => zombiePrefab.name;

    private UniqueIdGenerator uniqueIdGenerator;
    private LevelBuilder levelBuilder;
    private GameManager gm;

    private PlayerController playerController;

    private bool isGameOver = false;

    private float localTimeRemaining;
    private bool isCountdownRunning;
    private bool hasNotifiedServer;
    private bool gameOverPanelShown = false;

    public GameObject gameOverPanel; // Asigna el panel desde el inspector



    // Un tipo de variable especial, del cual no se puede leer hasta que el servidor lo actualice
    public NetworkVariable<int> playerNumber = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    private Action<ulong> HandleClientConnected;

    #endregion

    #region Unity game loop methods

    private void Awake()
    {
        Debug.Log("Despertando el nivel");

        Cursor.lockState = CursorLockMode.Locked; // Bloquea el cursor
        Cursor.visible = false; // Oculta el cursor

        // Obtener la referencia al UniqueIDGenerator
        uniqueIdGenerator = GetComponent<UniqueIdGenerator>();

        // Obtener la referencia al LevelBuilder
        levelBuilder = GetComponent<LevelBuilder>();

        // Obtener la referencia al GameManager
        gm = GameManager.Instance;

        Time.timeScale = 1f; // Asegurarse de que el tiempo no est� detenido
        if (levelBuilder != null)
        {
            levelBuilder.Build(gm.densidad.Value,gm.GetSeed());
            SpawnPoints = levelBuilder.GetSpawnPoints();
            gm.SetTotalCoins(levelBuilder.GetCoinsGenerated());
        }
    }

    private void Start()
    {
        minutes = gm.tiempo.Value;
        gameMode = gm.modo.Value;

        Debug.Log("Iniciando el nivel");
        // Buscar el objeto "CanvasPlayer" en la escena
        GameObject canvas = GameObject.Find("CanvasPlayer");
        if (canvas != null)
        {
            Debug.Log("Canvas encontrado");

            // Buscar el Panel dentro del CanvasHud
            Transform panel = canvas.transform.Find("PanelHud");
            if (panel != null)
            {
                // Buscar los TextMeshProUGUI llamados "HumansValue" y "ZombiesValue" dentro del Panel
                Transform humansTextTransform = panel.Find("HumansValue");
                Transform zombiesTextTransform = panel.Find("ZombiesValue");
                Transform gameModeTextTransform = panel.Find("GameModeConditionValue");

                if (humansTextTransform != null)
                {
                    humansText = humansTextTransform.GetComponent<TextMeshProUGUI>();
                }

                if (zombiesTextTransform != null)
                {
                    zombiesText = zombiesTextTransform.GetComponent<TextMeshProUGUI>();
                }

                if (gameModeTextTransform != null)
                {
                    gameModeText = gameModeTextTransform.GetComponent<TextMeshProUGUI>();
                }
            }
        }

        //gm.timeRemaining.Value = minutes * 60;
        gm.zombieNumber.OnValueChanged += OnPlayersChange;
        gm.humanNumber.OnValueChanged += OnPlayersChange;
        

        // Obtener los puntos de aparici�n y el n�mero de monedas generadas desde LevelBuilder


        //SpawnTeams();

        UpdateTeamUI();
    }

    private void OnPlayersChange(int a, int b)
    {
        UpdateTeamUI();
    }
    private void Update()
    {

        if (isCountdownRunning && !hasNotifiedServer)
        {
            localTimeRemaining = Mathf.Max(0, localTimeRemaining - Time.deltaTime);
            UpdateTimeUI();

            if (localTimeRemaining <= 0 && !hasNotifiedServer)
            {
                localTimeRemaining = 0;
                isCountdownRunning = false;
                hasNotifiedServer = true;

                if (GameManager.Instance != null)
                {
                    GameManager.Instance.NotifyTimeExpiredServerRpc();
                }
                UpdateTimeUI();
            }
        }

        /*
        if (gameMode == GameMode.Tiempo)
        {
            // L�gica para el modo de juego basado en tiempo
            //HandleTimeLimitedGameMode();
        }
        else if (gameMode == GameMode.Monedas)
        {
            // L�gica para el modo de juego basado en monedas
            //HandleCoinBasedGameMode();
        }*/

        if (Input.GetKeyDown(KeyCode.Z)) // Presiona "Z" para convertirte en Zombie
        {
            // Comprobar si el jugador actual est� usando el prefab de humano
            GameObject currentPlayer = GameObject.FindGameObjectWithTag("Player");
            if (currentPlayer != null && currentPlayer.name.Contains(playerPrefab.name))
            {
                ChangeToZombie();
            }
            else
            {
                Debug.Log("El jugador actual no es un humano.");
            }
        }
        //else if (Input.GetKeyDown(KeyCode.H)) // Presiona "H" para convertirte en Humano
        //{
        //    // Comprobar si el jugador actual est� usando el prefab de zombie
        //    GameObject currentPlayer = GameObject.FindGameObjectWithTag("Player");
        //    if (currentPlayer != null && currentPlayer.name.Contains(zombiePrefab.name))
        //    {
        //        ChangeToHuman();
        //    }
        //    else
        //    {
        //        Debug.Log("El jugador actual no es un zombie.");
        //    }
        //}
        //UpdateTeamUI();

        if (isGameOver && !gameOverPanelShown)
        {
            ShowGameOverPanel();
            gameOverPanelShown = true;
        }
    }

    private void UpdateTeamUI()
    {
        if (humansText != null)
        {
            humansText.text = $"{gm.humanNumber.Value}";
        }

        if (zombiesText != null)
        {
            zombiesText.text = $"{gm.zombieNumber.Value}";
        }
    }
    #endregion

    #region Team management methods



    private void ChangeToZombie()
    {
        return;
        GameObject currentPlayer = GameObject.FindGameObjectWithTag("Player");
        ChangeToZombie(currentPlayer, true);

        //Cambiar contadores
        //gm.ConvertHuman();

        
    }

    public void ChangeToZombie(GameObject human, bool enabled)
    {
        return;
        Debug.Log("Cambiando a Zombie");
        if (human == null) { Debug.LogError("No se encontr  el humano actual."); return; }

        // Guardar la posici n, rotaci n y uniqueID del humano actual
        Vector3 playerPosition = human.transform.position;
        Quaternion playerRotation = human.transform.rotation;
        string uniqueID = human.GetComponent<PlayerController>().uniqueID;

        // Destruir el humano actual
        RequestDestroyServerRpc(human);

        // Instanciar el prefab del zombie en la misma posici n y rotaci n
        GameObject zombie = Instantiate(zombiePrefab, playerPosition, playerRotation);
        if (enabled) { zombie.tag = "Player"; }

        // Obtener el componente PlayerController del zombie instanciado
        PlayerController playerController = zombie.GetComponent<PlayerController>();
        if (playerController == null) { Debug.LogError("PlayerController no encontrado en el zombie instanciado."); return; }

        playerController.enabled = enabled;
        playerController.isZombie = true; // Cambiar el estado a zombie
        playerController.uniqueID = uniqueID; // Mantener el identificador  nico

        UpdateTeamUI();

        if (enabled)
        {
            // Obtener la referencia a la c mara principal
            Camera mainCamera = Camera.main;

            if (mainCamera == null) { Debug.LogError("No se encontr  la c mara principal."); return; }

            // Obtener el script CameraController de la c mara principal
            CameraController cameraController = mainCamera.GetComponent<CameraController>();

            if (cameraController != null)
            {
                // Asignar el zombie al script CameraController
                cameraController.player = zombie.transform;
                playerController.enabled = enabled;
                playerController.isZombie = true; // Cambiar el estado a zombie
                playerController.uniqueID = uniqueID; // Mantener el identificador  nico
                //gm.ConvertHuman();
                UpdateTeamUI();

            }
        }
    }

    private void ChangeToHuman()
    {
        Debug.Log("Cambiando a Humano");
        return;

        // Obtener la referencia al jugador actual
        GameObject currentPlayer = GameObject.FindGameObjectWithTag("Player");

        if (currentPlayer != null)
        {
            // Guardar la posici n y rotaci n del jugador actual
            Vector3 playerPosition = currentPlayer.transform.position;
            Quaternion playerRotation = currentPlayer.transform.rotation;

            // Destruir el jugador actual
            RequestDestroyServerRpc(currentPlayer);

            // Instanciar el prefab del humano en la misma posici n y rotaci n
            GameObject human = Instantiate(playerPrefab, playerPosition, playerRotation);
            human.tag = "Player";

            // Obtener la referencia a la c mara principal
            Camera mainCamera = Camera.main;

            if (mainCamera != null)
            {
                // Obtener el script CameraController de la c mara principal
                CameraController cameraController = mainCamera.GetComponent<CameraController>();

                if (cameraController != null)
                {
                    // Asignar el humano al script CameraController
                    cameraController.player = human.transform;
                }

                // Obtener el componente PlayerController del humano instanciado
                playerController = human.GetComponent<PlayerController>();
                // Asignar el transform de la c mara al PlayerController
                if (playerController != null)
                {
                    playerController.enabled = true;
                    playerController.cameraTransform = mainCamera.transform;
                    playerController.isZombie = false; // Cambiar el estado a humano
                    //numberOfHumans++; // Aumentar el n mero de humanos
                    //numberOfZombies--; // Reducir el n mero de zombis
                }
                else
                {
                    Debug.LogError("PlayerController no encontrado en el humano instanciado.");
                }
            }
            else
            {
                Debug.LogError("No se encontr  la c mara principal.");
            }
        }
        else
        {
            Debug.LogError("No se encontr  el jugador actual.");
        }
    }

    public List<Vector3> GetSpawnPoints()
    {
        return SpawnPoints;
    }

    [ServerRpc]
    private void RequestDestroyServerRpc(GameObject currentPlayer)
    {
        if (NetworkManager.Singleton.IsServer)
        {
            GetComponent<NetworkObject>().Despawn();
        }
    }

    #endregion

    #region Modo de juego

    /*
    private void HandleTimeLimitedGameMode()
    {
        // Implementar la l gica para el modo de juego basado en tiempo
        if (isGameOver) return;

        // Decrementar gm.timeRemaining.Value basado en Time.deltaTime
        gm.timeRemaining.Value -= Time.deltaTime;

        // Comprobar si el tiempo ha llegado a cero
        if (gm.timeRemaining.Value <= 0)
        {
            isGameOver = true;
            gm.timeRemaining.Value = 0;
        }

        // Convertir gm.timeRemaining.Value a minutos y segundos
        int minutesRemaining = Mathf.FloorToInt(gm.timeRemaining.Value / 60);
        int secondsRemaining = Mathf.FloorToInt(gm.timeRemaining.Value % 60);

        //Condici n de victoria por sobrevivir
        if (gm.timeRemaining.Value <= 0)
        {
            gm.timeRemaining.Value = 0;
            isGameOver = true;
            Debug.Log(" Se acab  el tiempo, los Humanos han sobrevivido!");
            GameOver(" Se acab  el tiempo, los Humanos han sobrevivido!");
        }

    }
    */

    public void StartLocalCountdown(int durationSeconds)
    {
        localTimeRemaining = durationSeconds;
        isCountdownRunning = true;
        hasNotifiedServer = false;
        InitializeTimeUI();
    }

    private void InitializeTimeUI()
    {
        GameObject canvas = GameObject.Find("CanvasPlayer");
        if (canvas != null)
        {
            Transform panel = canvas.transform.Find("PanelHud");
            if (panel != null)
            {
                Transform timeTextTransform = panel.Find("TimeValue");
                if (timeTextTransform != null)
                {
                    timeText = timeTextTransform.GetComponent<TextMeshProUGUI>();
                    UpdateTimeUI();
                }
            }
        }
    }

    private void UpdateTimeUI()
    {
        if (timeText != null)
        {
            int minutes = Mathf.FloorToInt(localTimeRemaining / 60);
            int seconds = Mathf.FloorToInt(localTimeRemaining % 60);
            timeText.text = $"{minutes:00}:{seconds:00}";
        }
    }

    private void ShowGameOverPanel()
    {
        if (gameOverPanel != null)
        {
            Time.timeScale = 0f;
            gameOverPanel.SetActive(true); // Muestra el panel de pausa

            // Gesti n del cursor
            Cursor.lockState = CursorLockMode.None; // Desbloquea el cursor
            Cursor.visible = true; // Hace visible el cursor
        }
    }

    public void ReturnToMainMenu()
    {
        // Gesti n del cursor
        Cursor.lockState = CursorLockMode.None; // Bloquea el cursor
        Cursor.visible = true; // Oculta el cursor

        gm.ResetConvinientData();

        // Cargar la escena del men  principal
        SceneManager.LoadScene("MenuScene"); // Cambia "MenuScene" por el nombre de tu escena principal
    }

    public void GameOver(string message)
    {
        isGameOver = true;
        ShowGameOverPanel(message);
    }

    public void ShowGameOverPanel(string message)
    {
        if (gameOverPanel != null && !gameOverPanelShown)
        {
            gameOverPanelShown = true;

            // Activar/desactivar elementos según el tipo de mensaje
            bool isVictory = message.Contains("VICTORIA");
            Transform victoryElements = gameOverPanel.transform.Find("VictoryElements");
            Transform defeatElements = gameOverPanel.transform.Find("DefeatElements");

            if (victoryElements != null) victoryElements.gameObject.SetActive(isVictory);
            if (defeatElements != null) defeatElements.gameObject.SetActive(!isVictory);

            TextMeshProUGUI resultText = gameOverPanel.GetComponentInChildren<TextMeshProUGUI>();
            if (resultText != null)
            {
                resultText.text = message;
            }

            Time.timeScale = 0f;
            gameOverPanel.SetActive(true);

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    #endregion

}