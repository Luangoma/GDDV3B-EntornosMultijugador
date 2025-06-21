using System.Collections;
using System.Collections.Generic;
using TMPro;
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
    }

    // Update is called once per frame
    void Update()
    {
        str.SetText(gm.modo.Value.ToString());
    }
    public void changeMode()
    {
        if (gm.modo.Value == GameMode.Tiempo)
        {
            gm.modo.Value = GameMode.Monedas;
            gm.densidad.Value = slider.value;
            monedasUI.gameObject.SetActive(true);
        }
        else
        {
            gm.modo.Value = GameMode.Tiempo;
            gm.densidad.Value = 0;
            monedasUI.gameObject.SetActive(false);
        }
    }
}
