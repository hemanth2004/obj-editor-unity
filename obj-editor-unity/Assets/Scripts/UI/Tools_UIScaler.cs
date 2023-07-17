using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Tools_UIScaler : MonoBehaviour
{
    [SerializeField] private CanvasScaler canvasScaler;
    [SerializeField] private Slider slider;

    [SerializeField] private float initScaleFactor, finalScaleFactor;
    [SerializeField] private Vector2 initResolution, finalResolution;

    void Update()
    {
        ReferenceTypeUpdate();
    }

    private void ReferenceTypeUpdate()
    {
        Vector2 targetRes = new Vector2(initResolution.x + (finalResolution-initResolution).x*(1f-slider.value), initResolution.y + (finalResolution-initResolution).y*(1f-slider.value));
        canvasScaler.referenceResolution = Vector2.Lerp(canvasScaler.referenceResolution, targetRes, 15f * Time.deltaTime);
    }
    
    private void ScaleFactorTypeUpdate()
    {
        float targetSF = 0.4f + slider.value * (finalScaleFactor - initScaleFactor);
        canvasScaler.scaleFactor = Mathf.Lerp(canvasScaler.scaleFactor, targetSF, 15f * Time.deltaTime);
    }
}
