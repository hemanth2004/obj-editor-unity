using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EditorControl : MonoBehaviour
{
    private const string _objsite = "https://cs.wellesley.edu/~cs307/readings/obj-ojects.html";
    private const string _itchsite = "https://hmnt.itch.io/obj-editor";
    private const string _reposite = "https://github.com/hemanth2004/obj-editor-unity";
    private const string _opensite = "";
    [SerializeField] private Text_Editor textEditor;
    [SerializeField] private CameraMouseController mouseController;

    private void Update()
    {
            textEditor.enabled = mouseController.enabled == true && Input.GetMouseButton(1) ? false : true;
    }

    public void OpenSite()
    {

    }
}
