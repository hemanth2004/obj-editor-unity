using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class EditorControl : MonoBehaviour
{
    public UnityEvent fadeInEvent;
    private const string _objsite = "https://cs.wellesley.edu/~cs307/readings/obj-ojects.html";
    private const string _itchsite = "https://hmnt.itch.io/obj-editor";
    private const string _reposite = "https://github.com/hemanth2004/obj-editor-unity";
    private const string _opensite = "https://github.com/hemanth2004/obj-editor-unity#open-source-licences";

    [SerializeField] private Text_Editor textEditor;
    [SerializeField] private CameraMouseController mouseController;

    private void Start()
    {
        fadeInEvent.Invoke();
    }

    private void Update()
    {
            textEditor.enabled = mouseController.enabled == true && Input.GetMouseButton(1) ? false : true;
    }

    public void OpenSite(int i)
    {
        switch(i)
        {
            case 1: Application.OpenURL(_objsite); break;
            case 2: Application.OpenURL(_itchsite); break;
            case 3: Application.OpenURL(_reposite); break;
            case 4: Application.OpenURL(_opensite); break;
        }
    }
}
