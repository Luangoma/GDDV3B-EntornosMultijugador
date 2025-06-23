using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
//using UnityEditor.VersionControl;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : NetworkBehaviour
{
    [SerializeField] private GameObject humanPrefab;
    [SerializeField] private GameObject zombiePrefab;
    public NetworkVariable<bool> isGameOver = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private Dictionary<ulong, bool> readyStates = new Dictionary<ulong, bool>();
    public Dictionary<ulong, string> backupPlayerNames = new Dictionary<ulong, string>();
    private bool canJoin = true;
    private bool timeExpired = false;
    const int MINSEED = 0;
    const int MAXSEED = 25000;
    private List<Vector3> spawnPoints; // Centros de las habitaciones - las saca del level builder
    private NetworkManager nm;
    private UniqueIdGenerator uniqueIdGenerator;
    private int initialPlayerCount;

    //variable ultimo zombie

    #region Statics
    public static GameManager Instance { get; private set; }
    static NetworkVariableReadPermission rpEveryone = NetworkVariableReadPermission.Everyone;
    static NetworkVariableWritePermission wpServer = NetworkVariableWritePermission.Server;
    #endregion
    #region Variables compartidas
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
    // Menu
    public NetworkVariable<GameMode> modo = new NetworkVariable<GameMode>(GameMode.Monedas, rpEveryone, wpServer);
    public NetworkVariable<NetString> codigo = new NetworkVariable<NetString>(new NetString() { Value = "" }, rpEveryone, wpServer);
    public NetworkVariable<int> tiempo = new NetworkVariable<int>(0, rpEveryone, wpServer);
    public NetworkVariable<float> densidad = new NetworkVariable<float>(0, rpEveryone, wpServer);
    // Level
    public NetworkVariable<int> humanNumber = new(default, rpEveryone, wpServer);
    public NetworkVariable<int> zombieNumber = new(default, rpEveryone, wpServer);
    public NetworkVariable<int> totalCoins = new(default, rpEveryone, wpServer);
    public NetworkVariable<int> collectedCoins = new(default, rpEveryone, wpServer);
    // Data builder
    public NetworkVariable<int> mapSeed = new(default, rpEveryone, wpServer);
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
        densidad.Value = -5f;
        tiempo.Value = 5;
        nm = NetworkManager.Singleton;
        uniqueIdGenerator = new UniqueIdGenerator();
        if (IsServer)
        {
            nm.OnClientConnectedCallback += HandleClientConnected;
            nm.OnClientDisconnectCallback += HandleClientDisconnected;
            mapSeed.Value = UnityEngine.Random.Range(MINSEED, MAXSEED);
        }
        // A adir el host a readyStates
        ulong hostId = nm.LocalClientId;
        if (!readyStates.ContainsKey(hostId))
        {
            readyStates[hostId] = false;
            Debug.Log($"Host (clientId {hostId}) a adido a readyStates.");
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
    #region Other methods
    public void ResetConvinientData()
    {
        collectedCoins.Value = 0;
        if (nm.IsHost)
        {
            mapSeed.Value = UnityEngine.Random.Range(MINSEED, MAXSEED);
            //foreach (var item in readyStates.Keys) { readyStates[item] = false; }
        }
    }
    private void HandleClientConnected(ulong clientId)
    {
        if (!IsServer || canJoin) return;
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

        if (IsServer && !isGameOver.Value)
        {
            // Verificar si alguien abandonó durante la partida
            CheckWinConditionsServerRpc();
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

            int aux = 0;
            GameObject prefab = humanPrefab;
            foreach (var clientId in nm.ConnectedClientsIds)
            {
                bool isOriginalZombie = (aux >= humanNumber.Value);
                if (isOriginalZombie)
                {
                    prefab = zombiePrefab;
                }

                backupPlayerNames[clientId] = uniqueIdGenerator.GenerateUniqueID(); // Genera el nombre, que luego cada player lo asigna a su network variable en playercontroller
                GameObject player = Instantiate(prefab, spawnPoints[aux], Quaternion.identity);
                player.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);

                // Configurar WasOriginallyZombie para zombies iniciales
                {
                    PlayerController pc = player.GetComponent<PlayerController>();
                    if (pc != null)
                    {
                        pc.WasOriginallyZombie.Value = true;
                    }
                }

                StartTimeClientRpc(tiempo.Value * 60, new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
                });

                aux++;
            }
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
            canJoin = true;
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
        initialPlayerCount = players;
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
        Debug.Log($"Total de monedas generadas en el nivel: {totalCoins.Value}");

    }

    public void ConvertHuman(NetworkObject p)
    {
        if (!IsServer) return;
        // humanNumber.Value--;      // Esto ya lo hace el propio humano en el despawn
        // Destruir el humano
        // Guardarme sus coordenadas y rotacion


        PlayerController humanController = p.GetComponent<PlayerController>();
        if (humanController != null && !humanController.isZombie)
        {
            Vector3 position = p.transform.position;
            Quaternion rotation = p.transform.rotation;
            ulong clientId = p.OwnerClientId;

            p.Despawn();

            GameObject zombie = Instantiate(zombiePrefab, position, rotation);
            zombie.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);

            PlayerController zombieController = zombie.GetComponent<PlayerController>();
            if (zombieController != null)
            {
                zombieController.isZombie = true;
                zombieController.WasOriginallyZombie.Value = false;
                Debug.Log($"Nuevo zombie {clientId} - WasOriginallyZombie: {zombieController.WasOriginallyZombie.Value}");
            }

            zombieNumber.Value++;
            CheckWinConditionsServerRpc();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void TryConvertServerRpc(ulong id)
    {
        if (!IsServer) return;
        if (nm.SpawnManager.SpawnedObjects.TryGetValue(id, out NetworkObject obj))
        {
            PlayerController player = obj.GetComponent<PlayerController>();
            if (player != null && !player.isZombie)
            {
                //Convertir el humano
                ConvertHuman(obj);
            }
        }


    }
    [ServerRpc]
    public void NotifyCoinCollectedServerRpc()
    {
        collectedCoins.Value++;
        Debug.Log($"Monedas totales (global): {collectedCoins.Value}");

        // Verificar condici n de victoria
        CheckWinConditionsServerRpc();
    }

    [ServerRpc]
    public void NotifyPlayerTransformedServerRpc()
    {
        //Debug.Log("NotifyPlayerTransformedServerRpc llamado");


        //if (becameZombie)
        //{
        //    humanNumber.Value--;
        //    zombieNumber.Value++;
        //    Debug.Log($"Se transform  en zombie. Humanos: {humanNumber.Value}, Zombies: {zombieNumber.Value}");
        //}

        CheckWinConditionsServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    public void NotifyTimeExpiredServerRpc(ServerRpcParams rpcParams = default)
    {
        if (timeExpired || isGameOver.Value) return;

        timeExpired = true;
        isGameOver.Value = true;
        EndGame(VictoryType.HumanVictory, "¡Los Humanos ganan! Sobrevivieron el tiempo límite");
    }

    [ClientRpc]
    private void StartTimeClientRpc(int durationSeconds, ClientRpcParams clientRpcParams = default)
    {
        if (IsClient)
        {
            var levelManager = FindObjectOfType<LevelManager>();
            if (levelManager != null)
            {
                levelManager.StartLocalCountdown(durationSeconds);
            }
        }
    }

    public enum VictoryType
    {
        ZombieVictory,
        HumanVictory,
        Loss,
        GameAbandoned
    }


    [ServerRpc]
    private void CheckWinConditionsServerRpc()
    {
        if (isGameOver.Value) return;

        // Verificar abandono primero (comparar con número inicial de jugadores)
        if (nm.ConnectedClientsIds.Count < initialPlayerCount)
        {
            EndGame(VictoryType.GameAbandoned);
            return;
        }

        // Zombies ganan si no quedan humanos
        if (humanNumber.Value <= 0)
        {
            isGameOver.Value = true;

            // Primero identificar al último humano (si existe)
            PlayerController lastHuman = null;
            foreach (var client in nm.ConnectedClients)
            {
                var player = client.Value.PlayerObject.GetComponent<PlayerController>();
                if (!player.isZombie)
                {
                    lastHuman = player;
                    break;
                }
            }

            // Enviar mensajes
            foreach (var client in nm.ConnectedClients)
            {
                var player = client.Value.PlayerObject.GetComponent<PlayerController>();

                if (player == lastHuman)
                {
                    EndGame(VictoryType.Loss, "¡Derrota! Eres el último humano en ser convertido", client.Key);
                }
                else if (player.isZombie)
                {
                    Debug.Log($"Jugador {client.Key} - isZombie: {player.isZombie}, OriginallyZombie: {player.WasOriginallyZombie.Value}");

                    if (player.WasOriginallyZombie.Value)
                    {
                        Debug.Log($"Jugador {client.Key} es zombie original - VICTORIA TOTAL");
                        EndGame(VictoryType.ZombieVictory,
                              "¡VICTORIA TOTAL! Eliminaste a todos los humanos",
                              client.Key);
                    }
                    else
                    {
                        Debug.Log($"Jugador {client.Key} es zombie convertido - VICTORIA PARCIAL");
                        EndGame(VictoryType.ZombieVictory,
                              "¡VICTORIA PARCIAL! Fuiste convertido pero tu equipo ganó",
                              client.Key);
                    }
                }
            }
            return;
        }

        // Verificar condiciones según el modo de juego
        switch (modo.Value)
        {
            case GameMode.Monedas:
                if (collectedCoins.Value >= totalCoins.Value && totalCoins.Value > 0)
                {
                    isGameOver.Value = true;
                    foreach (var client in nm.ConnectedClients)
                    {
                        var player = client.Value.PlayerObject.GetComponent<PlayerController>();
                        if (!player.isZombie)
                        {
                            // Humanos ganan
                            EndGame(VictoryType.HumanVictory,
                                   "¡VICTORIA! Han recogido todas las monedas",
                                   client.Key);
                        }
                        else
                        {
                            // Zombies pierden
                            EndGame(VictoryType.Loss,
                                   "¡DERROTA! Los humanos recogieron todas las monedas",
                                   client.Key);
                        }
                    }
                }
                break;

            case GameMode.Tiempo:
                if (timeExpired)
                {
                    bool humansSurvived = humanNumber.Value > 0;

                    foreach (var client in nm.ConnectedClients)
                    {
                        var player = client.Value.PlayerObject.GetComponent<PlayerController>();
                        if (!player.isZombie)
                        {
                            EndGame(humansSurvived ? VictoryType.HumanVictory : VictoryType.Loss,
                                humansSurvived ? "¡Sobrevivieron el tiempo límite!" : "¡No lograron sobrevivir!",
                                client.Key);
                        }
                        else
                        {
                            EndGame(humansSurvived ? VictoryType.Loss : VictoryType.ZombieVictory,
                                "",
                                client.Key);
                        }
                    }
                }
                break;
        }
    }

    [ClientRpc]
    private void EndGameClientRpc(VictoryType victoryType, FixedString128Bytes additionalMessage, ClientRpcParams clientRpcParams = default)
    {
        // Obtener el PlayerController local
        PlayerController pc = NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject()?.GetComponent<PlayerController>();
        if (pc == null) return;

        string message = additionalMessage.ToString();

        // Si ya hay un mensaje específico (como para el último humano), usarlo
        if (!string.IsNullOrEmpty(message))
        {
            FindObjectOfType<LevelManager>()?.ShowGameOverPanel(message);
            return;
        }

        // Determinar mensaje basado en el tipo de victoria y el rol del jugador
        switch (victoryType)
        {
            case VictoryType.ZombieVictory:
                if (pc.WasOriginallyZombie.Value)
                {
                    message = "¡VICTORIA TOTAL ZOMBIS!\nEliminaste a todos los humanos";
                }
                else if (pc.isZombie)
                {
                    message = "¡VICTORIA PARCIAL ZOMBIS!\nFuiste convertido pero tu equipo ganó";
                }
                break;

            case VictoryType.HumanVictory:
                message = !pc.isZombie ?
               "¡VICTORIA HUMANOS!\n" + additionalMessage.ToString() :
               "¡DERROTA!\n" + additionalMessage.ToString();
                break;

            case VictoryType.Loss:
                message = "¡DERROTA!\n" + additionalMessage.ToString();
                break;

            case VictoryType.GameAbandoned:
                message = "PARTIDA CANCELADA\nUn jugador abandonó el juego";
                break;
        }

        if (!string.IsNullOrEmpty(message))
        {
            FindObjectOfType<LevelManager>()?.ShowGameOverPanel(message);
        }
    }

    public void EndGame(VictoryType victoryType, string additionalMessage = "", ulong clientId = 0)
    {
        isGameOver.Value = true;
        if (clientId == 0) // Notificar a todos
        {
            EndGameClientRpc(victoryType, new FixedString128Bytes(additionalMessage));
        }
        else // Notificar solo a un cliente
        {
            ClientRpcParams clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
            };
            EndGameClientRpc(victoryType, new FixedString128Bytes(additionalMessage), clientRpcParams);
        }
    }
    #endregion
}