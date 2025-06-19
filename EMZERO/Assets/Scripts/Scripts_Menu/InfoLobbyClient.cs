using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UIElements;

public class InfoLobbyClient : MonoBehaviour
{
    public TMP_Text text;
    private GameManager gm;
    private void Start()
    {
        gm = GameManager.Instance;
    }
    void Update()
    {
        StringBuilder str = new StringBuilder();
        str.AppendLine($"Tiempo de juego: {gm.tiempo.Value} minutos");
        if (gm.modo.Value == GameMode.Monedas) str.AppendLine($"Densidad de monedas: {gm.densidad.Value}%");
        text.SetText(str.ToString());
    }
}
