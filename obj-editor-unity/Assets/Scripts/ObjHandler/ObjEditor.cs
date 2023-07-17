using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System;

public class ObjEditor : MonoBehaviour
{
    [SerializeField] private Text_Editor textEditor;
    [SerializeField] private VertexTooltip vertexTooltip;
    [SerializeField] private MeshFilter modelMeshFilter;
    [SerializeField] private Transform highlightersParent, mainCamera;
    [SerializeField] private GameObject highlighterPrefab;
    [SerializeField] private UnityEngine.UI.Text linePositionDisplay;
    [SerializeField] private PrimitiveData[] primitiveDatas;

    private string prevText;
    private void LateUpdate()
    {
        linePositionDisplay.text = "Ln:" + textEditor.current_row + " Ch:" + textEditor.current_chr;
        if(prevText != textEditor.text)
        {
            Mesh m = LoadMeshFromObjText(textEditor.text);
            modelMeshFilter.mesh = m;
            modelMeshFilter.transform.GetChild(0).gameObject.GetComponent<MeshFilter>().mesh = m;
        }
        prevText = textEditor.text;
    }
    public Mesh LoadMeshFromObjText(string objText)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();
        List<int> normalIndices = new List<int>();
        List<int> uvIndices = new List<int>();

        string[] lines = objText.Split('\n');
        foreach (string line in lines)
        {
            if (line.StartsWith("v "))
            {
                string[] vertexData = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                float x = float.Parse(vertexData[1].Trim());
                float y = float.Parse(vertexData[2].Trim());
                float z = float.Parse(vertexData[3].Trim());
                vertices.Add(new Vector3(x, y, z));
            }
            else if (line.StartsWith("vn "))
            {
                string[] normalData = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                float nx = float.Parse(normalData[1].Trim());
                float ny = float.Parse(normalData[2].Trim());
                float nz = float.Parse(normalData[3].Trim());
                normals.Add(new Vector3(nx, ny, nz));
            }
            else if (line.StartsWith("vt "))
            {
                string[] uvData = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                float u = float.Parse(uvData[1].Trim());
                float v = float.Parse(uvData[2].Trim());
                uvs.Add(new Vector2(u, v));
            }
            else if (line.StartsWith("f "))
            {
                string[] faceData = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                for (int i = 1; i < faceData.Length; i++)
                {
                    string[] indices = faceData[i].Split(new[] { '/' });
                    int vertexIndex = int.Parse(indices[0]) - 1;
                    triangles.Add(vertexIndex);

                    // Check if texture coordinate index is provided
                    if (indices.Length >= 2 && !string.IsNullOrEmpty(indices[1]))
                    {
                        int uvIndex = int.Parse(indices[1]) - 1;
                        uvIndices.Add(uvIndex);
                    }

                    // Check if normal index is provided
                    if (indices.Length >= 3 && !string.IsNullOrEmpty(indices[2]))
                    {
                        int normalIndex = int.Parse(indices[2]) - 1;
                        normalIndices.Add(normalIndex);
                    }
                }
            }
        }

        LoadVertices(vertices.ToArray()); // Assuming you have a custom LoadVertices method

        Mesh mesh = new Mesh();
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();

        // Assign normals if available
        if (normals.Count > 0 && normalIndices.Count > 0)
        {
            List<Vector3> meshNormals = new List<Vector3>();
            foreach (int normalIndex in normalIndices)
            {
                meshNormals.Add(normals[normalIndex]);
            }
            mesh.normals = meshNormals.ToArray();
        }
        else
        {
            mesh.RecalculateNormals();
        }

        // Assign UV coordinates if available
        if (uvs.Count > 0 && uvIndices.Count > 0)
        {
            List<Vector2> meshUVs = new List<Vector2>();
            foreach (int uvIndex in uvIndices)
            {
                meshUVs.Add(uvs[uvIndex]);
            }
            mesh.uv = meshUVs.ToArray();
        }

        mesh.RecalculateBounds(); // Recalculate bounds to ensure correct rendering

        return mesh;
    }


    public void LoadVertices(Vector3[] vertexPositions)
    {
        vertexTooltip.UpdateVertices(vertexPositions);

        foreach(Transform t in highlightersParent)
        {
	        GameObject.Destroy(t.gameObject);
        }

        foreach(Vector3 pos in vertexPositions)
        {
            Instantiate(highlighterPrefab, pos, Quaternion.identity, highlightersParent).GetComponent<VertexHighlighter>().setValue(mainCamera);

        }
    }

    public void OnClickReplaceWith(int ind)
    {
        textEditor.text = primitiveDatas[ind].primitiveText;
    }
}


