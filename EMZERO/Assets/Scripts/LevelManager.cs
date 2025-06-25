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
        Cursor.lockState = CursorLockMode.Locked; // Bloquea el cursor
        Cursor.visible = false; // Oculta el cursor

        // Referencias
        uniqueIdGenerator = GetComponent<UniqueIdGenerator>();
        levelBuilder = GetComponent<LevelBuilder>();
        gm = GameManager.Instance;

        Time.timeScale = 1f; // Asegurarse de que el tiempo no este detenido
        if (levelBuilder != null)
        {
            var numberOfRooms = gm.humanNumber.Value + gm.zombieNumber.Value;
            levelBuilder.Build(gm.densidad.Value, gm.GetSeed(),numberOfRooms);
            SpawnPoints = levelBuilder.GetSpawnPoints();
            gm.SetTotalCoins(levelBuilder.GetCoinsGenerated());
        }
    }
    public void rebuildLevel()
    {
        
        // Referencias
        uniqueIdGenerator = GetComponent<UniqueIdGenerator>();
        levelBuilder = GetComponent<LevelBuilder>();
        gm = GameManager.Instance;

        Time.timeScale = 1f; // Asegurarse de que el tiempo no este detenido
        if (levelBuilder != null)
        {
            var numberOfRooms = gm.humanNumber.Value + gm.zombieNumber.Value;
            levelBuilder.Build(gm.densidad.Value, gm.GetSeed(), numberOfRooms);
            SpawnPoints = levelBuilder.GetSpawnPoints();
            gm.SetTotalCoins(levelBuilder.GetCoinsGenerated());
        }
    }
    private void Start()
    {
        minutes = gm.tiempo.Value;
        gameMode = gm.modo.Value;

        if (levelBuilder == null)
        {
            rebuildLevel();
                        return;
        }

        // Corregir isGameOver
        if (NetworkManager.Singleton.IsServer)
        {
            gm.isGameOver.Value = false;
        }

        // Buscar canvas en la escena
        GameObject canvas = GameObject.Find("CanvasPlayer");
        if (canvas != null)
        {
            // Buscar el Panel dentro del Canvas
            Transform panel = canvas.transform.Find("PanelHud");
            if (panel != null)
            {
                // Buscar los TextMeshProUGUI dentro del Panel
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

        // Suscripciones a eventos casos de cambios en el numero de jugadores
        gm.zombieNumber.OnValueChanged += OnPlayersChange;
        gm.humanNumber.OnValueChanged += OnPlayersChange;

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

            // Gestion del cursor
            Cursor.lockState = CursorLockMode.None; // Desbloquea el cursor
            Cursor.visible = true; // Hace visible el cursor
        }
    }

    public void ReturnToMainMenu()
    {
        // Gesti n del cursor
        Cursor.lockState = CursorLockMode.None; // Bloquea el cursor
        Cursor.visible = true; // Oculta el cursor

        gm.ResetConvinientDataServerRpc();

    }

    public void GameOver(string message)
    {
        isGameOver = true;
        ShowGameOverPanel(message);
    }

    public void ShowGameOverPanel(string message)
    {
        if (gameOverPanel != null)
        {
            Time.timeScale = 0f;
            gameOverPanel.SetActive(true);

            // Buscar el texto donde mostrar el mensaje
            TextMeshProUGUI resultText = gameOverPanel.GetComponentInChildren<TextMeshProUGUI>();

            if (resultText != null)
            {
                resultText.text = message;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    #endregion

}