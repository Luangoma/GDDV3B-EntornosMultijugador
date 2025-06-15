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

    public enum PantallaEnum : int
    {
        PantallaMenuInicio = 0,
        Pantallaclientehostseleccion = 1,
        Pantallacliente = 2,
        Pantallamodo = 3,
        Pantallamodomonedas = 4,
        Pantallamodotiempo = 5,
    }

    public GameObject actual;
    private string codigo;
    public ModeEnum modo;
    private float tiempo;
    private float densidad;

    public enum ModeEnum { Monedas, Tiempo }
    public void Awake()
    {
        Time.timeScale = 1f; // Asegúrate de que el tiempo está restaurado al cargar la escena
        actual = pantallas[(int)PantallaEnum.PantallaMenuInicio];
        gm = FindObjectOfType<GameManager>();
        
    }


    public GameObject EasyP(PantallaEnum pantalla)
    {
        return pantallas[(int)pantalla];
    }


    public void StartGame()
    {
        //SceneManager.LoadScene("GameScene"); // Cambia "MainScene" por el nombre de tu escena principal
        actual = EasyP(PantallaEnum.Pantallaclientehostseleccion);
        actual.gameObject.SetActive(true);
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
        modo = ModeEnum.Monedas;
        actual = EasyP(PantallaEnum.Pantallamodomonedas);
        actual.gameObject.SetActive(true);

    }
    public void PantallaModoTiempo()
    {
        modo = ModeEnum.Tiempo;
        actual = EasyP(PantallaEnum.Pantallamodotiempo);
        actual.gameObject.SetActive(true);
    }
    public void SetCodigoSala(string sala)
    {
        codigo = sala;
    }
    public void setTiempo(int tiempo)
    {
        this.tiempo = tiempo;
    }
    public void setDensidadMonedas(float densidad)
    {
        this.densidad = densidad;
    }
    public void StartGameMonedas() {
        SceneManager.LoadScene("GameScene");
    }
    public void playerReady() { 
        gm.SetReadyServerRpc(NetworkManager.Singleton.LocalClientId);
    }
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // Salir en el editor
#else
            Application.Quit(); // Salir en una build
#endif
    }
}
