using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MenuManager : MonoBehaviour
{
    public List<GameObject> pantallas = new List<GameObject>();
    public Stack<GameObject> historial = new Stack<GameObject>();
    public List<Button> buttons;
    public List<Button> atrasButtons;
    private GameManager gm;
    private NetworkManager nm;
    public GameObject botonListoObjClient;
    public GameObject botonListoObjHost;
    private Button botonListoHost;
    private Button botonListoClient;
    /*
     * PantallaMenuInicio = 0,
     * PantallaClienteHostSeleccion = 1,
     * PantallaCliente = 2,
     * PantallaLobyHost = 3,
     * PantallaLobyCliente = 4,
     * nameselector = 5
     */
    private List<int> screens = new List<int>() { 0, 1, 2, 3, 4 };
    private List<int> screenSelect = new List<int>() { 1, 3, 2, 4 };

    [SerializeField]
    private GameObject actual;
    public enum GameConnection : int
    {
        Cliente,
        Servidor,
        Host
    }
    public GameConnection conxion;

    #region NetworkBehaviour
    public void Awake()
    {
        Time.timeScale = 1f; // Aseg�rate de que el tiempo est� restaurado al cargar la escena
        actual = pantallas[screens[0]];
    }
    public void Start()
    {
        // Obtener boton listo para que no se puedan crear mas jugadores de los que se han seleccionado
        botonListoClient = botonListoObjClient.GetComponent<Button>();
        botonListoHost = botonListoObjHost.GetComponent<Button>();

        // Suscribirme a delegados
        botonListoClient.onClick.AddListener(OnMyButtonClientClick);
        botonListoHost.onClick.AddListener(OnMyButtonHostClick);

        gm = GameManager.Instance;
        nm = NetworkManager.Singleton;
        foreach (var item in pantallas) { item.gameObject.SetActive(false); }
        for (int i = 0; i < buttons.Count; i++)
        {
            int pantallaIndex = screens[screenSelect[i]]; // Es necesario guardar en variable
            buttons[i].onClick.AddListener(delegate { CambiarEscenaAdelante(); CambioPantalla(pantallaIndex); });
        }
        foreach (var item in atrasButtons) { item.onClick.AddListener(delegate { CambiarEscenaAtras(); }); }
        actual.gameObject.SetActive(true);
        
        ComprobarOnline();
    }

    private void OnMyButtonClientClick()
    {
        botonListoClient.interactable = false; // Desactivar el bot�n para evitar m�ltiples clics
    }
    private void OnMyButtonHostClick()
    {
        botonListoHost.interactable = false; // Desactivar el bot�n para evitar m�ltiples clics
    }   
    #endregion
    #region Navegacion entre pantallas
    public void ComprobarOnline()
    {
        if (nm.IsConnectedClient)
        {
            CambiarEscenaAdelante();
            if (nm.IsHost)
            {
                CambioPantalla(3);
            }
            else if (nm.IsClient)
            {
                CambioPantalla(4);
            }
            historial.Push(actual);
            pantallas[5].SetActive(true);

        }
    }
    public void StartGameFromMenuScene()
    {
        SceneManager.LoadScene("GameScene"); // Cambia "MainScene" por el nombre de tu escena principal
    }
    public void CambiarEscenaAdelante()
    {
        historial.Push(actual);
        actual.gameObject.SetActive(false);
    }
    public void CambioPantalla(int escena)
    {
        actual = pantallas[escena];
        actual.gameObject.SetActive(true);
    }
    public void CambiarEscenaAtras()
    {
        actual.gameObject.SetActive(false);
        actual = historial.Pop();
        actual.gameObject.SetActive(true);
    }
    #endregion
    #region Setters
    public void setCodigoSala(string sala)
    {
        gm.codigo.Value = new GameManager.NetString() { Value = sala };
    }
    public void setTiempo(int tiempo)
    {
        gm.tiempo.Value = tiempo;
    }
    public void setDensidadMonedas(float densidad)
    {
        gm.densidad.Value = densidad;
    }
    public void setModoMonedas()
    {
        gm.modo.Value = GameMode.Monedas;
    }
    public void setModoTiempo()
    {
        gm.modo.Value = GameMode.Tiempo;
    }
    public void setHost()
    {
        conxion = GameConnection.Host;
    }
    public void setCliente()
    {
        conxion = GameConnection.Cliente;
    }
    public void StartGameMonedas()
    {
        SceneManager.LoadScene("GameScene");
    }
    #endregion
    #region Conection
    public void playerReady()
    {
        gm.SetReadyServerRpc(nm.LocalClientId);
    }
    public void StartConections()
    {
        switch (conxion)
        {
            case GameConnection.Host:
                nm.StartHost();
                break;
            case GameConnection.Cliente:
                nm.StartClient();
                break;
            case GameConnection.Servidor:
                nm.StartServer();
                break;
        }
    }
    public void CloseConections()
    {
        nm.Shutdown();
    }
    #endregion
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // Salir en el editor
#else
            Application.Quit(); // Salir en una build
#endif
    }


    public void nameslectorview()
    {
        CambiarEscenaAdelante();
        pantallas[5].SetActive(true);
    }
    public void nameSelectOff()
    {
        pantallas[5].SetActive(false);
    }
}
