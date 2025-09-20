using UnityEngine;
using System.Linq;

public class BodyDistanceCalculator : MonoBehaviour
{
    [Tooltip("シーンのメインカメラをここに設定します。空のままだと自動で探します。")]
    public Transform mainCamera; 
    
    [HideInInspector] public GameObject[] bodySpheres; 
    [HideInInspector] public LineRenderer[] bodyLines; 

    void Awake()
    {
        if (mainCamera == null)
        {
            if (Camera.main != null)
            {
                mainCamera = Camera.main.transform;
                Debug.Log("Main Camera was not assigned. Found it automatically.", this);
            }
            else
            {
                Debug.LogError("Main Camera could not be found automatically. Please ensure your main camera has the 'MainCamera' tag.", this);
            }
        }
    }

    void LateUpdate()
    {
        if (mainCamera == null)
        {
            return;
        }

        float currentMinDistance = float.MaxValue;
        float currentMaxDistance = float.MinValue;

        if (bodySpheres != null)
        {
            foreach (GameObject sphere in bodySpheres)
            {
                if (sphere != null && sphere.activeInHierarchy)
                {
                    float dist = Vector3.Distance(mainCamera.position, sphere.transform.position);
                    currentMinDistance = Mathf.Min(currentMinDistance, dist);
                    currentMaxDistance = Mathf.Max(currentMaxDistance, dist);
                }
            }
        }

        if (bodyLines != null)
        {
            foreach (LineRenderer line in bodyLines)
            {
                if (line != null && line.gameObject.activeInHierarchy)
                {
                    Vector3[] positions = new Vector3[line.positionCount];
                    line.GetPositions(positions); 

                    foreach (Vector3 pos in positions)
                    {
                        float dist = Vector3.Distance(mainCamera.position, line.transform.TransformPoint(pos));
                        currentMinDistance = Mathf.Min(currentMinDistance, dist);
                        currentMaxDistance = Mathf.Max(currentMaxDistance, dist);
                    }
                }
            }
        }
        
        if (currentMinDistance != float.MaxValue)
        {
            Shader.SetGlobalFloat("_BodyMinDistance", currentMinDistance);
            Shader.SetGlobalFloat("_BodyMaxDistance", currentMaxDistance);
            
            // ★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★
            // ★★★ 最も重要なデバッグログです ★★★
            // ★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★
            Debug.Log($"UPDATING SHADER: MinDist={currentMinDistance}, MaxDist={currentMaxDistance}, CamPos={mainCamera.position}");
        }
    }
}