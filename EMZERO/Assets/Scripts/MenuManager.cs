using System;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{
    public List<GameObject> pantallas = new List<GameObject>();
    public Stack<GameObject> historial = new Stack<GameObject>();
    private GameManager gm;
    private NetworkManager nm;

    public enum PantallaEnum : int
    {
        PantallaMenuInicio = 0,
        Pantallaclientehostseleccion = 1,
        Pantallacliente = 2,
        Pantallamodo = 3,
        Pantallamodomonedas = 4,
        Pantallamodotiempo = 5,
        PantallaLoby = 6,
    }

    public enum GameConnection : int
    {
        Cliente = 0,
        Servidor = 1,
        Host = 2
    }

    public GameObject actual;
    public GameConnection conxion;
    public void Awake()
    {
        Time.timeScale = 1f; // Asegúrate de que el tiempo está restaurado al cargar la escena
        actual = pantallas[(int)PantallaEnum.PantallaMenuInicio];
    }
    public void Start()
    {
        gm = GameManager.Instance;
        nm = NetworkManager.Singleton;
    }



    #region Navegacion entre pantallas
    public GameObject EasyP(PantallaEnum pantalla)
    {
        return pantallas[(int)pantalla];
    }
    public void StartGameFromMenuScene()
    {
        SceneManager.LoadScene("GameScene"); // Cambia "MainScene" por el nombre de tu escena principal
        //actual = EasyP(PantallaEnum.Pantallaclientehostseleccion);
        //actual.gameObject.SetActive(true);
    }
    public void CambiarEscenaAdelante()
    {
        historial.Push(actual);
        actual.gameObject.SetActive(false);
    }
    public void CambiarEscenaAtras()
    {
        actual.gameObject.SetActive(false);
        actual = historial.Pop();
        actual.gameObject.SetActive(true);
    }
    #endregion
    #region Pantallas
    public void PantallaClienteHost()
    {
        //SceneManager.LoadScene("GameScene"); // Cambia "MainScene" por el nombre de tu escena principal
        actual = EasyP(PantallaEnum.Pantallaclientehostseleccion);
        actual.gameObject.SetActive(true);
    }
    public void PantallaCliente()
    {
        actual = EasyP(PantallaEnum.Pantallacliente);
        actual.gameObject.SetActive(true);
    }
    public void PantallaModo()
    {
        actual = EasyP(PantallaEnum.Pantallamodo);
        actual.gameObject.SetActive(true);
    }
    public void PantallaModoMonedas()
    {
        gm.modo = GameMode.Monedas;
        actual = EasyP(PantallaEnum.Pantallamodomonedas);
        actual.gameObject.SetActive(true);

    }
    public void PantallaModoTiempo()
    {
        gm.modo = GameMode.Tiempo;
        actual = EasyP(PantallaEnum.Pantallamodotiempo);
        actual.gameObject.SetActive(true);
    }
    public void PantallaLoby()
    {
        actual = EasyP(PantallaEnum.PantallaLoby); 
        actual.gameObject.SetActive(true);
    }
    #endregion
    #region Setters
    public void setCodigoSala(string sala)
    {
        gm.codigo = sala;
    }
    public void setTiempo(int tiempo)
    {
        gm.tiempo = tiempo;
    }
    public void setDensidadMonedas(float densidad)
    {
        gm.densidad = densidad;
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
        gm.SetReadyServerRpc(NetworkManager.Singleton.LocalClientId);
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
        switch (conxion)
        {
            case GameConnection.Host:
                // Poner funcion para desconectar
                //nm.StartHost();
                break;
            case GameConnection.Cliente:
                // Poner funcion para desconectar
                //nm.StartClient();
                break;
            case GameConnection.Servidor:
                // Poner funcion para desconectar
                //nm.StartServer();
                break;
        }
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
}
