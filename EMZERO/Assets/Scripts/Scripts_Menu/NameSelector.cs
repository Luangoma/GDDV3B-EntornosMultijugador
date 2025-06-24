using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class NameSelector : NetworkBehaviour
{
    public GameObject textosalida;
    public GameObject textoerrores;
    public TMP_InputField textentrada;
    private TMP_Text textsalida;
    private TMP_Text texterrores;
    private UniqueIdGenerator unigen;
    private GameManager gm;
    [SerializeField]
    void Start()
    {
        unigen = new UniqueIdGenerator();
        gm = GameManager.Instance;
        textsalida = textosalida.GetComponent<TMP_Text>();
        texterrores = textoerrores.GetComponent<TMP_Text>();
        textsalida.text = FormatName(unigen.GenerateUniqueID(null));
    }

    public void OnNombreValueChanged(FixedString32Bytes viejo, FixedString32Bytes nuevo)
    {
        Debug.Log("alalalalla");
    }
    private string FormatName(string name)
    {
        return $"Nombre actual:\n{name}";
    }

    public void SelectName()
    {
        var newnamestring = textentrada.text;
        var newname = new FixedString32Bytes(newnamestring);
        gm.SetNewNamePlayerNamesServerRpc(newname);
        textsalida.text = FormatName(newnamestring);
    }
    public void RandomName()
    {
        gm.SetRandomNamePlayerNamesServerRpc();
        textsalida.text = FormatName(gm.LastGeneratedName.Value.ToString());

    }
    IEnumerator BorrarTextoErrorTrasTiempo(float segundos)
    {
        yield return new WaitForSeconds(segundos);
        texterrores.text = "";
    }
    public void GetNameFromServer(bool libre, FixedString32Bytes name = new())
    {
        if (!libre)
        {
            texterrores.text = $"El nombre seleccionado\nya ha sido elegido";
            StartCoroutine(BorrarTextoErrorTrasTiempo(5f));
        }
        else
        {
        }
    }
}
