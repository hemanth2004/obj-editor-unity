using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VertexPooler : MonoBehaviour
{

    [SerializeField] private Dictionary<string, Queue<GameObject>> poolDictionary;
    [SerializeField] private Transform highlightersParent;
    [HideInInspector] public Transform mainCamera;

    [System.Serializable]
    public class Pool
    {
        public string tag;
        public GameObject prefab;
        public int size;
    }

    public List<Pool> pools;

    private void Start()
    {
        poolDictionary = new Dictionary<string, Queue<GameObject>>();

        foreach (Pool p in pools)
        {
            Queue<GameObject> objectPool = new Queue<GameObject>();
            for (int i = 0; i < p.size; i++)
            {
                GameObject g = Instantiate(p.prefab, Vector3.zero, Quaternion.identity, highlightersParent);
                g.GetComponent<VertexHighlighter>().setValue(mainCamera);
                g.SetActive(false);
                objectPool.Enqueue(g);
            }

            poolDictionary.Add(p.tag, objectPool);
        }
    }

    public GameObject SpawnFromPool(string tag, Vector3 position, Quaternion rotation)
    {
        if (poolDictionary.ContainsKey(tag))
        {
            Debug.LogWarning("tag doesnt exist");
            return null;
        }

        GameObject objectToSpawn = poolDictionary[tag].Dequeue();

        objectToSpawn.SetActive(true);
        objectToSpawn.transform.position = position;
        objectToSpawn.transform.rotation = rotation;

        poolDictionary[tag].Enqueue(objectToSpawn);

        return objectToSpawn;

    }

    public void ClearVertices()
    {
        foreach (Pool p in pools)
        {
            if (poolDictionary.ContainsKey(p.tag))
            {
                Queue<GameObject> objectPool = poolDictionary[p.tag];
                while (objectPool.Count > 0)
                {
                    GameObject pooledObject = objectPool.Dequeue();
                    pooledObject.SetActive(false);
                    ReturnObjectToPool(p.tag, pooledObject);
                }
            }
        }
    }

    public void ReturnObjectToPool(string tag, GameObject returnedObject)
    {
        if (poolDictionary.ContainsKey(tag))
        {
            returnedObject.SetActive(false);
            poolDictionary[tag].Enqueue(returnedObject);
        }
        else
        {
            Debug.LogWarning("Invalid pool tag: " + tag);
        }
    }
}
