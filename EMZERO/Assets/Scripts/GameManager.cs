using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : NetworkBehaviour
{

    [SerializeField] private GameObject humanPrefab;
    [SerializeField] private GameObject zombiePrefab;

    private Dictionary<ulong, bool> readyStates = new Dictionary<ulong, bool>();


    // Centros de las habitaciones
    private int humanNumber;
    private int zombieNumber;

    private List<Vector3> spawnPoints;

    private int nextSpawnIndex = 0;
    public static GameManager Instance { get; private set; }
    public GameMode modo;
    public string codigo;
    public float tiempo;
    public float densidad;
    private NetworkManager nm;

    public NetworkVariable<int> humansAlive = new NetworkVariable<int>();
    public NetworkVariable<int> zombiesAlive = new NetworkVariable<int>();
    public NetworkVariable<int> coinsCollected = new NetworkVariable<int>();
    public NetworkVariable<bool> isGameOver = new NetworkVariable<bool>();
    public NetworkVariable<GameMode> currentGameMode = new NetworkVariable<GameMode>();
    public NetworkVariable<float> remainingTime = new NetworkVariable<float>();

    public void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(this);
        }
        else
        {
            Destroy(this); // Esto evita m�ltiples instancias
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        Debug.Log($"Modo de juego inicializado: {currentGameMode.Value}");

        nm = NetworkManager.Singleton;
        if (IsServer)
        {
            nm.OnClientConnectedCallback += HandleClientConnected;
            nm.OnClientDisconnectCallback += HandleClientDisconnected;
            currentGameMode.Value = modo; // Asigna el modo de juego
            remainingTime.Value = tiempo * 60; //Minutos a segundos


        }
        // A�adir el host a readyStates
        ulong hostId = nm.LocalClientId;
        if (!readyStates.ContainsKey(hostId))
        {
            readyStates[hostId] = false;
            Debug.Log($"Host (clientId {hostId}) a�adido a readyStates.");
        }
    }

    void Update()
    {
        if (IsServer && !isGameOver.Value && currentGameMode.Value == GameMode.Tiempo)
        {
            remainingTime.Value -= Time.deltaTime;
            if (remainingTime.Value <= 0)
            {
                remainingTime.Value = 0;
                EndGame("Los humanos han sobrevivido");
            }
        }
    }

    public override void OnDestroy()
    {
        if (IsServer && nm != null)
        {
            nm.OnClientConnectedCallback -= HandleClientConnected;
            nm.OnClientDisconnectCallback -= HandleClientDisconnected;
        }

    }

    public override void OnNetworkSpawn()
    {
        if (humanPrefab == null || zombiePrefab == null)
        {
            Debug.LogError("playerPrefab no asignado en GameManager.");
            return;
        }
    }

    private void HandleClientConnected(ulong clientId)
    {
        if (!IsServer) return;
        readyStates[clientId] = false;
        Debug.Log($"Client {clientId} connected");
    }

    public void SpawnClient(ulong clientId, Vector3 spawnPos, GameObject prefab)
    {
        if (!IsServer) return; // Solo el servidor debe spawnear

        Debug.Log($"Spawning {clientId} player");

        // Solo el servidor ejecuta esto

        GameObject player = Instantiate(prefab, spawnPos, Quaternion.identity);
        player.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);
    }
    private void HandleClientDisconnected(ulong clientId)
    {
        if (readyStates.ContainsKey(clientId))
        {
            int idx = GetClientIndex(clientId);
            readyStates.Remove(clientId);
        }
    }
    private int GetClientIndex(ulong clientId)
    {
        // Puedes usar una lista de clientIds para mantener el orden
        int idx = 0;
        foreach (var id in readyStates.Keys)
        {
            if (id == clientId) return idx;
            idx++;
        }
        return -1;
    }
    private bool AllReady()
    {
        // Solo cuenta los clientId realmente conectados
        foreach (var clientId in nm.ConnectedClientsIds)
        {
            if (!readyStates.ContainsKey(clientId) || !readyStates[clientId])
                return false;
        }
        // Debe haber al menos 2 jugadores (host + al menos un cliente)
        return nm.ConnectedClientsIds.Count > 0;
    }

    private void OnNetworkSceneLoaded(string sceneName, LoadSceneMode mode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        if (sceneName == "GameScene" && nm.IsServer)
        {
            CreateTeams();
            CreateSpawnPoints();

            LevelBuilder levelBuilder = FindObjectOfType<LevelBuilder>();
            if (levelBuilder != null)
            {
                Debug.Log($"Total de monedas generadas: {levelBuilder.GetCoinsGenerated()}");
            }

            // Aqu� el servidor puede spawnear a todos los jugadores
            int aux = 0;
            GameObject prefab = humanPrefab;
            foreach (var clientId in nm.ConnectedClientsIds)
            {
                if (aux >= humanNumber) prefab = zombiePrefab;

                SpawnClient(clientId, spawnPoints[aux], prefab);

                aux++;
            }

            //Debug.LogError("GameManager no encontrado en la nueva escena.");
        }
        nm.SceneManager.OnLoadEventCompleted -= OnNetworkSceneLoaded;
    }


    // Llamado por el cliente al pulsar "Listo"
    [ServerRpc(RequireOwnership = false)]
    public void SetReadyServerRpc(ulong localClientId, ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        readyStates[clientId] = true;
        int idx = GetClientIndex(clientId);
        Debug.Log($"Client {clientId} is ready.");
        if (AllReady())
        {
            Debug.Log("All clients are ready. Starting game...");
            Debug.Log("Estados de ready antes de iniciar partida:");
            foreach (var kvp in readyStates)
                Debug.Log($"Client {kvp.Key} ready: {kvp.Value}");

            Debug.Log("All clients are ready. Starting game...");
            nm.SceneManager.OnLoadEventCompleted += OnNetworkSceneLoaded;
            nm.SceneManager.LoadScene("GameScene", LoadSceneMode.Single);
        }
    }

    private void CreateTeams()
    {
        int players = nm.ConnectedClientsList.Count;
        humanNumber = (players % 2 == 0) ? players / 2 : (players / 2) + 1;
        zombieNumber = players / 2;

        humansAlive.Value = humanNumber;
        zombiesAlive.Value = zombieNumber;
    }

    private void CreateSpawnPoints()
    {
        LevelManager lm = FindObjectOfType<LevelManager>();
        spawnPoints = lm.GetSpawnPoints();
    }

    [ServerRpc]
    public void NotifyCoinCollectedServerRpc()
    {

        coinsCollected.Value++;
        Debug.Log($"Monedas totales (global): {coinsCollected.Value}");

        // Verificar condici�n de victoria
        CheckWinConditionsServerRpc();
    }

    [ClientRpc]
    private void UpdateCoinsClientRpc(int newCount)
    {
        // Sincronizar el conteo en todos los jugadores
        foreach (var player in FindObjectsOfType<PlayerController>())
        {
            player.CoinsCollected.Value = newCount;
            player.UpdateCoinUI(newCount);
        }
    }

    [ServerRpc]
    public void NotifyPlayerTransformedServerRpc(bool becameZombie)
    {
        Debug.Log("NotifyPlayerTransformedServerRpc llamado");


        if (becameZombie)
        {
            humansAlive.Value--;
            zombiesAlive.Value++;
            Debug.Log($"Se transform� en zombie. Humanos: {humansAlive.Value}, Zombies: {zombiesAlive.Value}");
        }
        else
        {
            humansAlive.Value++;
            zombiesAlive.Value--;
            Debug.Log($"Se transform� en humano. Humanos: {humansAlive.Value}, Zombies: {zombiesAlive.Value}");
        }

        CheckWinConditionsServerRpc();
    }


    [ServerRpc]
    private void CheckWinConditionsServerRpc()
    {
        Debug.Log($"DENTRO DE LAS WC- Humanos: {humansAlive.Value}, Zombies: {zombiesAlive.Value}");

        //if (isGameOver.Value) return;

        // Zombies ganan si no quedan humanos
        if (humansAlive.Value <= 0)
        {
            EndGame("�Los Zombies ganan!");
            Debug.Log("Zombies ganan");

            return;
        }

        // Verificar condiciones seg�n el modo de juego
        //if (currentGameMode.Value == GameMode.Monedas)
        //{
            LevelBuilder levelBuilder = FindObjectOfType<LevelBuilder>();
            if (levelBuilder == null)
            {
                Debug.LogError("LevelBuilder no encontrado!");
                return;
            }

            int totalCoins = levelBuilder.GetCoinsGenerated();
            Debug.Log($"Verificando monedas: {coinsCollected.Value}/{totalCoins} (Modo: {currentGameMode.Value})");

            if (coinsCollected.Value >= totalCoins && totalCoins > 0)
            {
                Debug.Log("�Condici�n de victoria cumplida! Humanos ganan");
                EndGame("�Los Humanos ganan! Han recogido todas las monedas");
                return;
            }
        //}
        else if (currentGameMode.Value == GameMode.Tiempo)
        {
            // La condici�n de tiempo se maneja en Update
        }
    }

    [ClientRpc]
    private void EndGameClientRpc(string message)
    {
        FindObjectOfType<LevelManager>().ShowGameOverPanel(message);
    }

    public void EndGame(string message)
    {
        isGameOver.Value = true;
        EndGameClientRpc(message);
    }
}