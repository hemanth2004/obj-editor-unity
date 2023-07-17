using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MeshUI : MonoBehaviour
{
    public int id;
    public Transform t;

    public Text nameUI, meshID_UI;
    [SerializeField] private Image thumbnailUI;

    public void Load()
    {
        
        RefreshID();
        
        
        Texture2D thumbnail = RuntimePreviewGenerator.GenerateModelPreview(t,64,64,false,true);
        Sprite sprite = Sprite.Create(thumbnail, new Rect(0, 0, thumbnail.width, thumbnail.height), new Vector2(0.5f,0.5f));
        thumbnailUI.sprite = sprite;
    }
    public void RefreshID()
    {
        nameUI.text = t.name; // +":"+ id.ToString();
        gameObject.name = "meshUI:" + id;
        meshID_UI.text = id.ToString();
    }

    public void Highlight()
    {
        t.GetComponent<Outline>().enabled = true;
    }
    public void Unhighlight()
    {
        t.GetComponent<Outline>().enabled = false;
    }
}
