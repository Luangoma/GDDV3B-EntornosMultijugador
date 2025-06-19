using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class changeModeMenuText : MonoBehaviour
{
    public TMP_Text str;
    GameManager gm;
    // Start is called before the first frame update
    void Start()
    {
        gm = GameManager.Instance;
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
        }
        else
        {
            gm.modo.Value = GameMode.Tiempo;
        }
    }
}
