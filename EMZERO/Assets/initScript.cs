using UnityEngine;
using UnityEngine.SceneManagement;

public class initScript : MonoBehaviour
{
    // Start is called before the first frame update
    private void Start()
    {
        // Cambiar a la escena "MainMenu" al iniciar el juego
        SceneManager.LoadScene("MenuScene");
    }

    // Update is called once per frame
    private void Update()
    {
    }
}