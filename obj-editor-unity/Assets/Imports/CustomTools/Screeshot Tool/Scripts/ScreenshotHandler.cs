using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;

[Serializable]
public class ScreenshotHandler : MonoBehaviour
{
    public bool IsTransparent = false;
    public bool OpenFileDirecoty = true;
    private TextureFormat transp = TextureFormat.ARGB32;
    private TextureFormat nonTransp = TextureFormat.RGB24;

    public KeyCode ShotKey = KeyCode.Space;

    public Resolution[] Resolutions;



    private void LateUpdate()
    {
        if (Input.GetKeyDown(ShotKey))
        {
            if (Resolutions.Length == 0)
            {
                Debug.LogWarning("no resolution found !");
                return;



            }
            else
            {
                for (int i = 0; i < Resolutions.Length; i++)
                {
                    if (Resolutions[i].Width == 0 || Resolutions[i].Height == 0)
                    {
                        Debug.LogWarning("Resolution can't be 0 !");
                        return;
                    }
                    else
                    {
                        Capture(Resolutions[i].Width, Resolutions[i].Height, 1);
                    }
                }
            }
        }
    }

    private void Capture(int width, int height, int enlargeCOEF)
    {
        TextureFormat textForm = nonTransp;

        if (IsTransparent)
            textForm = transp;
        RenderTexture rt = new RenderTexture(width * enlargeCOEF, height * enlargeCOEF, 24);
        Camera.main.targetTexture = rt;
        Texture2D screenShot = new Texture2D(width * enlargeCOEF, height * enlargeCOEF, textForm, false);
        Camera.main.Render();
        RenderTexture.active = rt;
        screenShot.ReadPixels(new Rect(0, 0, width * enlargeCOEF, height * enlargeCOEF), 0, 0);
        Camera.main.targetTexture = null;
        RenderTexture.active = null;
        Destroy(rt);
        byte[] bytes = screenShot.EncodeToPNG();
        string filename = ScreenshotName("ANDROID+", (width * enlargeCOEF).ToString(), (height * enlargeCOEF).ToString());

        if (!Directory.Exists(Application.persistentDataPath + "/../screenshots/"))
            Directory.CreateDirectory(Application.persistentDataPath + "/../screenshots/");

        System.IO.File.WriteAllBytes(filename, bytes);
        Debug.Log(string.Format("Took screenshot to: {0}", filename));

        if (OpenFileDirecoty)
        {
            Process.Start(Application.persistentDataPath + "/../screenshots/");
        }
    }

    private string ScreenshotName(string platform, string width, string height)
    {
        return string.Format("{0}/../screenshots/" + "_" + platform + "screen_{1}x{2}_{3}.png", Application.persistentDataPath, width, height, System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
    }

}