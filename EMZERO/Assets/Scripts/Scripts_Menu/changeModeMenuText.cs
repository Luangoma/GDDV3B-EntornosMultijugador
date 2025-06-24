using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class changeModeMenuText : MonoBehaviour
{
    public TMP_Text str;
    public GameObject monedasUI;
    public Slider slider;
    GameManager gm;
    // Start is called before the first frame update
    void Start()
    {
        gm = GameManager.Instance;
        str.SetText(gm.modo.Value.ToString());
        changeMode();
    }

    // Update is called once per frame
    void Update()
    {
        str.SetText(gm.modo.Value.ToString());
    }
    public void changeMode()
    {
        if (NetworkManager.Singleton.IsServer)
        {
            if (gm.modo.Value == GameMode.Tiempo)
            {
                gm.modo.Value = GameMode.Monedas;
                gm.densidad.Value = slider.value * 5;
                monedasUI.gameObject.SetActive(true);
            }
            else
            {
                gm.modo.Value = GameMode.Tiempo;
                gm.densidad.Value = -1;
                monedasUI.gameObject.SetActive(false);
            }
        }
    }
}
