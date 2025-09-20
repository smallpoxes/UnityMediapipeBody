using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SphereEffect : MonoBehaviour
{
    public bool SpeedSizerSwitch;
    public GameObject TcpObject; // TcpCommunicatorを持つGameObjectの参照
    public GameObject PipeObject;
    private PipeServer pipeServer;
    private float originalLandmarkScale;
    private Vector3 originalHeadScale;
    public Vector3 modifiedLandmarkScale;//for investigation
    public Vector3 modifiedHeadScale;//for investigation
    public Vector3 speed;//for investigation
    private TcpCommunicator tcpCommunicator; // TcpCommunicatorの参照
    public float SpeedThreshold;
    private Vector3[] centerList = new Vector3[2];
    private GameObject[] sphereObjects; //これは直接代入して行けばいいから、使わないかも
    // Start is called before the first frame update
    void Start()
    {
        tcpCommunicator = TcpObject.GetComponent<TcpCommunicator>();
        pipeServer = PipeObject.GetComponent<PipeServer>();
        originalLandmarkScale = pipeServer.landmarkScale;
        originalHeadScale = pipeServer.headPrefab.transform.localScale;
        //sphereObjects = this.child
    }

    // Update is called once per frame
    void Update()
    {

        SpeedSizer(SpeedSizerSwitch, tcpCommunicator);

    }

    Vector3 calcAbsolute(Vector3 vec)
    {
        return new Vector3(
            Mathf.Abs(vec.x),
            Mathf.Abs(vec.y),
            Mathf.Abs(vec.z)
        );
    }

    void SpeedSizer(bool Switch, TcpCommunicator tcpCommunicator)
    {
        //headをいれなあかん
        //headtagと、bodytagを付与して、headtagのときは、headのtransformを参照せなあかん
        //headtransformとbodytransformを分ける必要がある
        //Vector3 headTransform;
        if (Switch)
        {
            centerList[1] = centerList[0];
            centerList[0] = new Vector3(
                tcpCommunicator.features != null && tcpCommunicator.features.Length > 0 ? tcpCommunicator.features[0] : 0f,
                tcpCommunicator.features != null && tcpCommunicator.features.Length > 1 ? tcpCommunicator.features[1] : 0f,
                tcpCommunicator.features != null && tcpCommunicator.features.Length > 2 ? tcpCommunicator.features[2] : 0f
            );
            float scale = 100.0f; // 特徴量に基づいてスケールを計算（例: 1.0から3.0の範囲）
            speed = calcAbsolute(centerList[0] - centerList[1]) * scale;
            
            foreach (Transform child in transform.GetChild(0))
            {
                if (child.CompareTag("head"))
                {
                    child.localScale = new Vector3(
                        (SpeedThreshold < speed.x) ? originalHeadScale.x + (speed.x ) : originalHeadScale.x,
                        (SpeedThreshold < speed.y) ? originalHeadScale.y + (speed.y ) : originalHeadScale.y,
                        (SpeedThreshold < speed.z) ? originalHeadScale.z + (speed.z ) : originalHeadScale.z
                    );
                    modifiedHeadScale = child.localScale;
                }
                else if (child.CompareTag("body"))
                {
                    child.localScale = new Vector3(
                        (SpeedThreshold < speed.x) ? originalLandmarkScale + (speed.x ) : originalLandmarkScale,
                        (SpeedThreshold < speed.y) ? originalLandmarkScale + (speed.y ) : originalLandmarkScale,
                        (SpeedThreshold < speed.z) ? originalLandmarkScale + (speed.z ) : originalLandmarkScale
                    );
                    modifiedLandmarkScale = child.localScale;
                }
                else
                {
                    continue;
                }

            }
            
        }
    }
}
