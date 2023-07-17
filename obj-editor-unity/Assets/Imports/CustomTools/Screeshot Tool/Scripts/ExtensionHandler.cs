
using UnityEngine;

public static class ExtensionHandler
{
    public static string Extension(PictureExtension extension)
    {
        return "." + extension.ToString().ToLower();
    }

    public static byte[] ByteArray(Texture2D texture,PictureExtension extension)
    {
        if(extension==PictureExtension.EXR)
        {
            return ImageConversion.EncodeToEXR(texture);
        }
        else if(extension==PictureExtension.JPG)
        {
            return ImageConversion.EncodeToJPG(texture);
        }
        else if(extension==PictureExtension.PNG)
        {
            return ImageConversion.EncodeToPNG(texture);
        }
        else if(extension==PictureExtension.TGA)
        {
            return ImageConversion.EncodeToTGA(texture);
        }
        else
        {
            Debug.LogError("Not possible to encode 'Texture2D' to byte array ... ");
            return null;
        }
    }
}
