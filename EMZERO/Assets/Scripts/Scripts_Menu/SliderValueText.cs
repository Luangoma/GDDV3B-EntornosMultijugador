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
        slider.value = 5;
        textField.SetText("5");
    }
    public void HandleSliderValueOnChangeBy5(float value)
    {
        var tmp = value * 5;
        textField.SetText($"{tmp.ToString()} % monedas");
    }
    public void HandleSliderValueOnChange(float value)
    {
        textField.SetText($"{value.ToString()} minutos");
    }
}
