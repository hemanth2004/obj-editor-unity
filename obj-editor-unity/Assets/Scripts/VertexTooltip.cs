using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class VertexTooltip : MonoBehaviour
{
    public class VertexP
    {
        public int ind;
        public Vector3 wrld, srelp;

        public VertexP(int i, Vector3 _w, Vector3 _s)
        {
            ind = i; wrld = _w; srelp = _s;
        }
    }
    // We want to store world positions of each vertex,
    // screen position of each vertex (relative to pivot)
    // and constantly get screen position of the mouse (relative to pivot)
    // We want to convert world pos to screen pos for each vertex (vectors)
    // We also want to find the closest vertex to the mouse position using on screen (pivot relative) distances
    private const float _rendertextureheight = 720, _rendertexturewidth = 1280;


    [SerializeField] private RectTransform tooltipPrefab, pivotPositionForSelectRect;
    [SerializeField] private Transform highlightersParent;
    [SerializeField] private Camera cam;

    public Vector2 Ratio;
    public CanvasScaler canvasScaler;
    public RectTransform selectRect;


    private List<VertexP> vertexPositions = new List<VertexP>();

    private Vector3 prevPos,prevRot;

    private void Update()
    {
        if (vertexPositions.Count == 0)
        {
            tooltipPrefab.gameObject.SetActive(false);
            Debug.LogWarning("Array of positions is empty.");
            return;
        }
        if(prevRot != cam.transform.eulerAngles || prevPos!= cam.transform.position)
        {
            UpdateVerticesForCam();
            prevPos = cam.transform.position;
            prevRot = cam.transform.eulerAngles;
        }

        

        VertexP closestVertexOnScreen = FindClosestPoint(GetMouseRelative());

        tooltipPrefab.gameObject.SetActive(true);
        tooltipPrefab.anchoredPosition = closestVertexOnScreen.srelp;
        tooltipPrefab.GetChild(0).GetChild(0).GetComponent<TMPro.TMP_Text>().text =
            "<color=grey><size=25>(" + closestVertexOnScreen.ind + ")</size></color> "
            + closestVertexOnScreen.wrld.x.ToString() + ", "
            + closestVertexOnScreen.wrld.y.ToString() + ", "
            + closestVertexOnScreen.wrld.z.ToString();


        /*
        float widthInPixels = selectRect.rect.width / canvasScaler.referenceResolution.x * Screen.width;
        float heightInPixels = selectRect.rect.height / canvasScaler.referenceResolution.y * Screen.height;

        Vector2 pivotPosition = new Vector2(selectRect.position.x - widthInPixels / 2f, selectRect.position.y - heightInPixels / 2f);

        //Vector2 relativeMousePosition = new Vector2(Input.mousePosition.x - pivotPosition.x, Input.mousePosition.y - pivotPosition.y);
        Vector2 relativeMousePosition = new Vector2(screenPos.x - pivotPosition.x, screenPos.y - pivotPosition.y);

        Vector2 mousePositionRatio = new Vector2(relativeMousePosition.x / widthInPixels, relativeMousePosition.y / heightInPixels);


        Vector3 closestPosition = FindClosestPoint(vertexPositions, relativeMousePosition);

        
        */


    }

    public void UpdateVertices(Vector3[] _vertexPositions)
    {
        vertexPositions.Clear();
        int i = 1;
        foreach(Vector3 v in _vertexPositions)
        {   
            vertexPositions.Add(new VertexP(i, v, GetScreenFromWorld(v)));
            i++;
        }
    }

    public void UpdateVerticesForCam()
    {
        foreach (VertexP v in vertexPositions)
        {
            v.srelp = GetScreenFromWorld(v.wrld);
        }
    }
    

    private Vector3 GetScreenFromWorld(Vector3 position)
    {
        Vector3 screenPos = cam.WorldToScreenPoint(position);
        Vector2 screenPosRatio = new Vector2(screenPos.x / _rendertexturewidth, screenPos.y / _rendertextureheight);

        return new Vector3(screenPosRatio.x * selectRect.rect.width, screenPosRatio.y * selectRect.rect.height, 0f);

    }
    private VertexP FindClosestPoint(Vector3 targetPosition)
    {
        //calculate closest using screen mouse pos but return the world pos of the closest vertex

        VertexP closestPoint = vertexPositions[0];
        float closestDistance = Vector3.Distance(closestPoint.srelp + pivotPositionForSelectRect.position, targetPosition);

        for (int i = 1; i < vertexPositions.Count; i++)
        {
            float distance = Vector3.Distance(vertexPositions[i].srelp + pivotPositionForSelectRect.position, targetPosition);

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestPoint = vertexPositions[i];
            }
        }

        return closestPoint;
    }

    private Vector3 GetMouseRelative()
    {
        return new Vector3((Input.mousePosition.x / Screen.width) * canvasScaler.gameObject.GetComponent<RectTransform>().rect.width, (Input.mousePosition.y / Screen.height) * canvasScaler.gameObject.GetComponent<RectTransform>().rect.height, 0f);// pivotPositionForSelectRect.position;
    }

}
