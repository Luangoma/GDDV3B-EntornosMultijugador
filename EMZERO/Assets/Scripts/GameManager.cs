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

    private ulong? lastConvertedHumanId = null;

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
    public NetworkVariable<int> ZombiesDesconectados = new(default, rpEveryone, wpServer);
    public NetworkVariable<int> HumanosDesconectados = new(default, rpEveryone, wpServer);
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
    [ServerRpc]
    public void ResetConvinientDataServerRpc()
    {
        if (nm.IsServer)
        {
            collectedCoins.Value = 0;
            mapSeed.Value = UnityEngine.Random.Range(MINSEED, MAXSEED);
            //foreach (var item in readyStates.Keys) { readyStates[item] = false; }
            readyStates.Clear(); // Limpiar los estados de ready para reiniciar el juego
            // Reiniciar el contador de monedas y zombies
            humanNumber.Value = 0;
            zombieNumber.Value = 0;
            timeExpired = false;
            lastConvertedHumanId = null;
            isGameOver.Value = false;
                
            // Despawnear a todos los jugadores
            foreach (var clientId in nm.ConnectedClientsIds)
            {
                if (nm.SpawnManager.SpawnedObjects.TryGetValue(clientId, out NetworkObject obj))
                {
                    obj.Despawn();
                }
            }
            // Despawnear objetos de red que no sean jugadores
            foreach (var obj in FindObjectsOfType<NetworkObject>())
            {
                if (obj != null && obj.IsSpawned && obj.gameObject.tag != "Player")
                {
                    obj.Despawn();
                }
            }
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


        Debug.Log("ConvertHuman iniciado - Jugador: " + p.OwnerClientId);


        PlayerController humanController = p.GetComponent<PlayerController>();
        if (humanController != null && !humanController.isZombie)
        {            
            //Debug.Log($"Convirtiendo humano {p.OwnerClientId} a zombie...");
            lastConvertedHumanId = p.OwnerClientId;
            //Debug.Log($"Último humano : {lastConvertedHumanId}");

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
                //Debug.Log($"Nuevo zombie {clientId} - WasOriginallyZombie: {zombieController.WasOriginallyZombie.Value}");

                NotifyLastClientRpc(clientId, new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
                });
            }

            Debug.Log($"Jugador {p.OwnerClientId} convertido. Estado actual - isZombie: {zombieController.isZombie}, WasOriginallyZombie: {zombieController.WasOriginallyZombie.Value}");

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
    public void NotifyPlayerTransformedServerRpc(ServerRpcParams rpcParams = default)
    {
        //Debug.Log("NotifyPlayerTransformedServerRpc llamado");


        //if (becameZombie)
        //{
        //    humanNumber.Value--;
        //    zombieNumber.Value++;
        //    Debug.Log($"Se transform  en zombie. Humanos: {humanNumber.Value}, Zombies: {zombieNumber.Value}");
        //}

        // variable condicion desconexion
        CheckWinConditionsServerRpc();
    }

    [ClientRpc]
    private void NotifyLastClientRpc(ulong convertedPlayerId, ClientRpcParams clientRpcParams = default)
    {
        if (NetworkManager.Singleton.LocalClientId == convertedPlayerId)
        {
            Debug.Log("¡Has sido convertido en zombie!");
            // Aquí puedes añadir efectos visuales/sonidos
        }
    }


    [ServerRpc(RequireOwnership = false)]
    public void NotifyTimeExpiredServerRpc(ServerRpcParams rpcParams = default)
    {
        if (timeExpired || isGameOver.Value) return;

        timeExpired = true;

        //isGameOver.Value = true;
        //EndGame(VictoryType.HumanVictory, "¡Los Humanos ganan! Sobrevivieron el tiempo límite");
        CheckWinConditionsServerRpc();
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

    //Tipos de victoria
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

        // Verificar abandono
        if (zombieNumber.Value == 0 && zombieNumber.Value <= ZombiesDesconectados.Value)
        {
            EndGameForAll(VictoryType.GameAbandoned, "¡Los zombies abandonaron la partida!");
            return; ;
        }
        else if(humanNumber.Value == 0 && humanNumber.Value < HumanosDesconectados.Value)
        {
            EndGameForAll(VictoryType.GameAbandoned, "¡Los humanos abandonaron la partida!");
            return;
        }

        // Zombies ganan si no quedan humanos independientemente del modo de juego
        if (humanNumber.Value <= 0)
        {
            DetermineZombieVictory();
            return;
        }

        // Verificar condiciones según el modo de juego
        switch (modo.Value)
        {
            case GameMode.Monedas:
                if (collectedCoins.Value >= totalCoins.Value && totalCoins.Value > 0)
                {
                    DetermineCoinVictory();
                }
                break;

            case GameMode.Tiempo:
                if (timeExpired)
                {
                    DetermineTimeVictory();
                }
                break;
        }
    }

    private void DetermineZombieVictory()
    {
        isGameOver.Value = true;

        // Lista para evitar duplicados
        List<ulong> playersNotified = new List<ulong>();

        // Notificar primero al último humano convertido (si existe)
        if (lastConvertedHumanId.HasValue)
        {
            //Debug.Log($"Procesando último humano convertido: {lastConvertedHumanId}");
            //Si hay ultimo humano convertido
            if (lastConvertedHumanId != null)
            {                
                    //Debug.Log($"ENVIANDO DERROTA al último humano: {lastConvertedHumanId.Value}");

                    //Se le dice solo al ultimo humano que ha perdido
                    EndGameForPlayer(VictoryType.Loss,
                        "¡Derrota! Fuiste el último humano en ser convertido",
                        lastConvertedHumanId.Value);
                //Evita que el ultimo humano reciba doble pantalla de victoria al ser ultimo humano y zombie que gana
                playersNotified.Add(lastConvertedHumanId.Value);                
            }
        }

        //Notificar a los otros zombies (excluyendo al ya notificado)
        foreach (var client in nm.ConnectedClients)
        {
            if (playersNotified.Contains(client.Key)) continue;

            var player = client.Value.PlayerObject.GetComponent<PlayerController>();

            if (player.isZombie)
            {
                string message = player.WasOriginallyZombie.Value
                    ? "¡VICTORIA TOTAL! Eliminaste a todos los humanos"
                    : "¡VICTORIA PARCIAL! Fuiste convertido pero tu equipo ganó";

                //Debug.Log($"Enviando victoria a zombie {client.Key}: {message}");
                EndGameForPlayer(VictoryType.ZombieVictory, message, client.Key);
            }
        }
    }

    private void DetermineCoinVictory()
    {
        isGameOver.Value = true;

        //Recorrer todos los usuarios
        foreach (var client in nm.ConnectedClients)
        {
            //ver si son humanos o zombies
            var player = client.Value.PlayerObject.GetComponent<PlayerController>();
            //los zombies reciben el primer mensaje, los humanos el segundo
            string message = player.isZombie
                ? "¡DERROTA! Los humanos recogieron todas las monedas"
                : "¡VICTORIA! Han recogido todas las monedas";

            // Asigna el tipo de victoria/derrota según el equipo (zombie/humano
            VictoryType victoryType = player.isZombie ? VictoryType.Loss : VictoryType.HumanVictory;

            // Notifica individualmente al jugador con su resultado
            EndGameForPlayer(victoryType, message, client.Key);
        }
    }

    private void DetermineTimeVictory()
    {
        isGameOver.Value = true;

        foreach (var client in nm.ConnectedClients)
        {
            var player = client.Value.PlayerObject.GetComponent<PlayerController>();

            if (player.isZombie)
            {
                // Zombies reciben mensaje de derrota
                EndGameForPlayer(VictoryType.Loss,
                    "¡Derrota! Los humanos sobrevivieron el tiempo límite",
                    client.Key);
            }
            else
            {
                // Humanos reciben mensaje de victoria
                EndGameForPlayer(VictoryType.HumanVictory, "¡VICTORIA! Sobreviviste al tiempo límite", client.Key);
            }
        }
    }

    private void EndGameForAll(VictoryType victoryType, string message = "")
    {
        isGameOver.Value = true;
        EndGameClientRpc(victoryType, new FixedString128Bytes(message));
    }

    
    private void EndGameForPlayer(VictoryType victoryType, string message, ulong clientId)
    {
        Debug.Log($"PREPARANDO MENSAJE PARA {clientId}: {message}");

        ClientRpcParams clientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
        };
        EndGameClientRpc(victoryType, new FixedString128Bytes(message), clientRpcParams);
    }

    [ClientRpc(RequireOwnership = false)]
    private void EndGameClientRpc(VictoryType victoryType, FixedString128Bytes message, ClientRpcParams clientRpcParams = default)
    {
        // Obtener el PlayerController local
        PlayerController pc = NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject()?.GetComponent<PlayerController>();
        if (pc == null) return;

        string finalMessage = message.ToString();

        //TryDespawnServerRpc(pc.NetworkObjectId); // Intentar despawnear el jugador local

        // Mostrar el panel de fin de juego
        FindObjectOfType<LevelManager>()?.ShowGameOverPanel(finalMessage);
    }


    //[ServerRpc(RequireOwnership = false)]
    //public void TryDespawnServerRpc(ulong id)
    //{
    //    if (!IsServer) return;
    //    if (nm.SpawnManager.SpawnedObjects.TryGetValue(id, out NetworkObject obj))
    //    {
    //        PlayerController player = obj.GetComponent<PlayerController>();
    //        if (player != null && !player.isZombie)
    //        {
    //            //Convertir el humano
    //            ConvertHuman(obj);
    //        }
    //    }


    //}

    #endregion

    //public void ResetGame()
    //{
    //    // Reset variables de estado
    //    isGameOver.Value = false;
    //    timeExpired = false;
    //    lastConvertedHumanId = null;
    //    collectedCoins.Value = 0;
    //    // ... cualquier otra variable relevante

    //    // Despawning de objetos de red (ejemplo para monedas y enemigos)
    //    foreach (var obj in FindObjectsOfType<NetworkObject>())
    //    {
    //        // Evita despawnear jugadores si quieres mantenerlos
    //        if (obj != null && obj.IsSpawned && obj.gameObject.tag != "Player")
    //        {
    //            obj.Despawn();
    //        }
    //    }

    //    // Notifica a los clientes para que reseteen su estado local
    //    ResetClientRpc();
    //}

    //[ClientRpc]
    //private void ResetClientRpc()
    //{
    //    LevelManager levelManager = FindObjectOfType<LevelManager>();
    //    if (levelManager != null)
    //    {
    //        levelManager.ResetLocalState();
    //    }
    //}

}