using UnityEngine;

public class GenerateInstances : MonoBehaviour
{
    public int Row = 10;
    public int Column = 10;
    public GameObject prefab; // The prefab to instantiate
    
    private int m_InstanceCount;
    private GameObject[] instances;
    
    void Start()
    {
        m_InstanceCount = Row * Column;
        InitGameObjects();
    }
    
    private void InitGameObjects()
    {
        instances = new GameObject[m_InstanceCount];
        var parentPosition = transform.position;
        
        for (int r = 0; r < Row; r++)
        {
            for (int c = 0; c < Column; c++)
            {
                int index = r * Column + c;
                
                // Create position identical to the original method
                var position = parentPosition + new Vector3(1 * c, 1 * r, 0);
                
                // Instantiate GameObject with no rotation and default scale (equivalent to TRS matrix)
                GameObject instance = Instantiate(prefab, position, Quaternion.identity);
                instance.transform.parent = this.transform;
                instance.name = $"Instance_{r}_{c}";
                
                // Apply random color just like in the GPU version
                Renderer renderer = instance.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = Random.ColorHSV();
                }
                
                instances[index] = instance;
            }
        }
    }
}