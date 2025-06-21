using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : NetworkBehaviour
{
    const int MINSEED = 0;
    const int MAXSEED = 25000;

    [SerializeField] private GameObject humanPrefab;
    [SerializeField] private GameObject zombiePrefab;

    private Dictionary<ulong, bool> readyStates = new Dictionary<ulong, bool>();

    // Centros de las habitaciones - las saca del level builder
    private List<Vector3> spawnPoints;

    public static GameManager Instance { get; private set; }
    public NetworkVariable<GameMode> modo = new NetworkVariable<GameMode>(GameMode.Monedas, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<NetString> codigo = new NetworkVariable<NetString>(new NetString() { Value = "" }, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> tiempo = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> densidad = new NetworkVariable<float>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkManager nm;

    #region Variables compartidas

    public NetworkVariable<int> humanNumber = new(writePerm: NetworkVariableWritePermission.Server, readPerm: NetworkVariableReadPermission.Everyone);
    public NetworkVariable<int> zombieNumber = new(writePerm: NetworkVariableWritePermission.Server, readPerm: NetworkVariableReadPermission.Everyone);

    public NetworkVariable<int> totalCoins = new(writePerm: NetworkVariableWritePermission.Server, readPerm: NetworkVariableReadPermission.Everyone);
    public NetworkVariable<int> collectedCoins = new(writePerm: NetworkVariableWritePermission.Server, readPerm: NetworkVariableReadPermission.Everyone);
    public NetworkVariable<float> timeRemaining = new(writePerm: NetworkVariableWritePermission.Server, readPerm: NetworkVariableReadPermission.Everyone);
    public NetworkVariable<int> mapSeed = new(writePerm: NetworkVariableWritePermission.Server, readPerm: NetworkVariableReadPermission.Everyone);

    public struct NetString : INetworkSerializable, IEquatable<NetString>
    {
        public string Value;

        public bool Equals(NetString other)
        {
            return Value == other.Value;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            if (serializer.IsReader)
            {
                var reader = serializer.GetFastBufferReader();
                reader.ReadValueSafe(out Value);
            }
            else
            {
                var writter = serializer.GetFastBufferWriter();
                writter.WriteValueSafe(Value);
            }
        }
    }

    #endregion
    #region NetworkBehaviour
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
    public void Start()
    {
        Instance.densidad.Value = 5f;
        Instance.tiempo.Value = 5;
        nm = NetworkManager.Singleton;
        if (IsServer)
        {
            nm.OnClientConnectedCallback += HandleClientConnected;
            nm.OnClientDisconnectCallback += HandleClientDisconnected;
            mapSeed.Value = UnityEngine.Random.Range(MINSEED, MAXSEED);
        }
        // A�adir el host a readyStates
        ulong hostId = nm.LocalClientId;
        if (!readyStates.ContainsKey(hostId))
        {
            readyStates[hostId] = false;
            Debug.Log($"Host (clientId {hostId}) a�adido a readyStates.");
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
    #endregion
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
            // Aqu� el servidor puede spawnear a todos los jugadores
            int aux = 0;
            GameObject prefab = humanPrefab;
            foreach (var clientId in nm.ConnectedClientsIds)
            {
                if (aux >= humanNumber.Value) prefab = zombiePrefab;

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
        humanNumber.Value = (players % 2 == 0) ? players / 2 : (players / 2) + 1;
        zombieNumber.Value = players / 2;
    }

    private void CreateSpawnPoints()
    {
        LevelManager lm = FindObjectOfType<LevelManager>();
        spawnPoints = lm.GetSpawnPoints();
    }

    public int GetSeed()
    {
        return mapSeed.Value;
    }

    public void SetTotalCoins(int coins)
    {
        totalCoins.Value = coins;
    }

    public void ConvertHuman(NetworkObject p)
    {
        if (!IsServer) return;
        //humanNumber.Value--;      // Esto ya lo hace el propio humano en el despawn
        // Destruir el humano
        // Guardarme sus coordenadas y rotacion
        Vector3 position = p.transform.position;
        Quaternion rotation = p.transform.rotation;
        ulong clientId = p.OwnerClientId;
        p.Despawn();
        // Crear el zombie
        GameObject zombie = Instantiate(zombiePrefab, position, rotation);
        zombie.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);

        // Añadir a la cuenta de zombies
        zombieNumber.Value++;

    }

    [ServerRpc(RequireOwnership = false)]
    public void TryConvertServerRpc(ulong id)
    {
        if (!IsServer) return;
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(id, out NetworkObject obj))
        {
            PlayerController player = obj.GetComponent<PlayerController>();
            if (player != null && !player.isZombie)
            {
                //Convertir el humano
                ConvertHuman(obj);

            }

        }
    }

}

