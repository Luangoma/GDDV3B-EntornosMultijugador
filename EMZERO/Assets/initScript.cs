using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class initScript : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        // Cambiar a la escena "MainMenu" al iniciar el juego
        UnityEngine.SceneManagement.SceneManager.LoadScene("MenuScene");

    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
