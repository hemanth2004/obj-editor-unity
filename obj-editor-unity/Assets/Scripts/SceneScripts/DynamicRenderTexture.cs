using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class DynamicRenderTexture : MonoBehaviour
{
    [SerializeField] private Camera cam;
    [SerializeField] private RenderTexture renderTexture;
    [SerializeField] private RectTransform renderRect;
    
    [SerializeField] private Vector2 rect;

    void Update()
    {
        rect = new Vector2( renderRect.rect.width,  renderRect.rect.height);
    }
}
