using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SliderValueText : MonoBehaviour
{
    public Slider slider;
    public TMP_Text textField;
    public void Awake()
    {
        textField.SetText("5");
    }
    public void Start()
    {
        slider.value = GameManager.Instance.tiempo.Value;
    }
    public void HandleSliderValueOnChangeBy5(float value)
    {
        var tmp = value * 5;
        GameManager.Instance.densidad.Value = tmp;
        textField.SetText($"{tmp.ToString()} % monedas");
    }
    public void HandleSliderValueOnChange(float value)
    {
        GameManager.Instance.tiempo.Value = (int)value;
        textField.SetText($"{value.ToString()} minutos");
    }
}
