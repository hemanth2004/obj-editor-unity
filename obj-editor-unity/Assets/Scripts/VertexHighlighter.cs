using UnityEngine;

public class VertexHighlighter : MonoBehaviour
{
    private Transform cam;
    public void setValue(Transform _cam)
    {
        cam = _cam;
    }

    private void Update()
    {
        transform.rotation = cam.rotation;
    }
}