using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : NetworkBehaviour
{

    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private List<Vector3> spawnPoints; // Asigna desde el inspector o genera dinámicamente
    private Dictionary<ulong, bool> readyStates = new Dictionary<ulong, bool>();


    // Centros de las habitaciones
    private List<Vector3> humanSpawnPoints = new List<Vector3>
    {
        new Vector3(4f, 2f, 4f),
        new Vector3(8f, 2f, 4f),
        new Vector3(4f, 2f, 8f),
        new Vector3(8f, 2f, 8f)
    };

    private int nextSpawnIndex = 0;
    public static GameManager Instance { get; private set; }
    public GameMode modo;
    public string codigo;
    public float tiempo;
    public float densidad;
    private NetworkManager nm;

    public void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(this);
        }
        else
        {
            Destroy(this); // Esto evita múltiples instancias
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        nm = NetworkManager.Singleton;
        if (IsServer)
        {
            nm.OnClientConnectedCallback += HandleClientConnected;
            nm.OnClientDisconnectCallback += HandleClientDisconnected;
        }
        // Añadir el host a readyStates
        ulong hostId = nm.LocalClientId;
        if (!readyStates.ContainsKey(hostId))
        {
            readyStates[hostId] = false;
            Debug.Log($"Host (clientId {hostId}) añadido a readyStates.");
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
    // Update is called once per frame
    void Update()
    {

    }
    public override void OnNetworkSpawn()
    {
        if (playerPrefab == null)
        {
            Debug.LogError("playerPrefab no asignado en GameManager.");
            return;
        }
    }

    private void HandleClientConnected(ulong clientId)
    {
        if (!IsServer) return;
        //SpawnClient(clientId);
        readyStates[clientId] = false;
        Debug.Log($"Client {clientId} connected");
    }

    public void SpawnClient(ulong clientId)
    {
        if (!IsServer) return; // Solo el servidor debe spawnear

        int playerIndex = (int)clientId % humanSpawnPoints.Count;
        Vector3 spawnPos = humanSpawnPoints[playerIndex];

        Debug.Log($"Spawning {clientId} player");

        // Solo el servidor ejecuta esto
        if (nextSpawnIndex >= humanSpawnPoints.Count)
        {
            Debug.LogWarning("No hay más habitaciones libres para spawnear jugadores.");
            return;
        }
        nextSpawnIndex++;
        GameObject player = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
        player.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);
    }

    public void StartGame()
    {
        Debug.Log("Starting game with all players ready.");
        foreach (var clientId in nm.ConnectedClientsIds)
        {
            SpawnClient(clientId);
        }

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
        return nm.ConnectedClientsIds.Count > 1;
    }

    private void OnNetworkSceneLoaded(string sceneName, LoadSceneMode mode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        if (sceneName == "GameScene" && nm.IsServer)
        {
            // Aquí el servidor puede spawnear a todos los jugadores
            foreach (var clientId in nm.ConnectedClientsIds)
                SpawnClient(clientId);
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
}
