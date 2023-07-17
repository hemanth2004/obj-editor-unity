using UnityEngine;
using System.IO;
using UnityEngine.UI;
using Lean.Gui;

public class TextFileCreator : MonoBehaviour
{
    private const string objextension = ".obj";
    private Text_Editor textEditor;

    [SerializeField] private Material StandardMat;
    [SerializeField] private LeanWindow window;
    [SerializeField] private RawImage exportthumbnail;
    [SerializeField] private Transform exportmodel;
    [SerializeField] private InputField exportNameField;

    private void Start()
    {
        textEditor = GetComponent<ObjEditor>().textEditor;
        exportname = exportNameField.text;
    }
    private string exportname;
    private string GetPlatformPath()
    {
        string platformPath = "";

        // Check the current platform and assign the appropriate path
        switch (Application.platform)
        {
            case RuntimePlatform.WindowsPlayer:
                platformPath = Path.Combine(Application.dataPath, "..\\");
                break;
            case RuntimePlatform.OSXPlayer:
                platformPath = Path.Combine(Application.dataPath, "../");
                break;
            case RuntimePlatform.LinuxPlayer:
                platformPath = Path.Combine(Application.dataPath, "../");
                break;
            default:
                Debug.LogWarning("Platform not supported for saving files.");
                break;
        }

        return platformPath;
    }

    public void SaveTextToBuildFolder(string fileName, string text)
    {
        string platformPath = GetPlatformPath();
        string filePath = Path.Combine(platformPath, fileName);
        File.WriteAllText(filePath, text);

        Debug.Log("Text file saved to: " + filePath);
    }

    public void ClickOnSave()
    {
        GameObject clone = new GameObject("thumbnailClone");
        clone.AddComponent<MeshRenderer>();
        clone.AddComponent<MeshFilter>();
        clone.GetComponent<MeshRenderer>().material = StandardMat;
        clone.GetComponent<MeshFilter>().mesh = gameObject.GetComponent<ObjEditor>().mesh;

        Texture2D t = RuntimePreviewGenerator.GenerateModelPreview(clone.transform, 388, 654, false, true);
        exportthumbnail.texture = t;
        Destroy(clone.gameObject);
    }

    public void OnEditName()
    {
        exportname = exportNameField.text;
    }
    public void FinalSave()
    {
        SaveTextToBuildFolder(exportname + objextension, textEditor.text);
    }
}
