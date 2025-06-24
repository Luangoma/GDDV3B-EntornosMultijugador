using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    public GameObject pausePanel; // Asigna el panel desde el inspector

    private bool isPaused = false;

    void Update()
    {
        // Detecta si el jugador presiona la tecla Escape o Pausa
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }
    }

    public void PauseGame()
    {
        isPaused = true;
        pausePanel.SetActive(true); // Muestra el panel de pausa
        if (!NetworkManager.Singleton.IsServer)
        {
            Time.timeScale = 0f; // Detiene el tiempo en el juego solo si no es el servidor
        }

        // Gestión del cursor
        Cursor.lockState = CursorLockMode.None; // Desbloquea el cursor
        Cursor.visible = true; // Hace visible el cursor
    }

    public void ResumeGame()
    {
        isPaused = false;
        pausePanel.SetActive(false); // Oculta el panel de pausa
        if (!NetworkManager.Singleton.IsServer)
        {
            Time.timeScale = 1f; // Reactiva el tiempo en el juego si era un cliente
        }

        // Gestión del cursor
        Cursor.lockState = CursorLockMode.Locked; // Bloquea el cursor
        Cursor.visible = false; // Oculta el cursor
    }

    public void QuitGame()
    {
        // Asegurar de que el tiempo está restaurado antes de salir
        Time.timeScale = 1f;
        NetworkManager.Singleton.Shutdown();    // Si el cliente deja la partida se desconecta de la red
        SceneManager.LoadScene("MenuScene"); // Cambia "MainMenu" por el nombre de tu escena principal
    }
}
