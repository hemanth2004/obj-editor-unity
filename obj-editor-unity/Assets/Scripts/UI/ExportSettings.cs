using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ExportSettings : MonoBehaviour
{
    
    [SerializeField] private AspectRatioFitter fitter;
    [SerializeField] private InputField xRatioField, yRatioField;
    [SerializeField] private Camera viewCamera, mainCamera;
    [SerializeField] private RenderTexture[] supportedRenderTextures;
    public Vector2 exportResolution;

    public void OnChangeAspectRatio()
    {
        exportResolution.x = float.Parse(xRatioField.text);
        exportResolution.y = float.Parse(yRatioField.text);

        fitter.aspectRatio = exportResolution.x/exportResolution.y;
    }
}
