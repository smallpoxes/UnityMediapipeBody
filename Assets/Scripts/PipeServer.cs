using System.Collections;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Linq;
using System.Text;
using System.Threading;
using UnityEngine;

public class PipeServer : MonoBehaviour
{
    public Transform parent;
    public GameObject linePrefab;
    public GameObject linePrefab2;
    public GameObject headPrefab;
    public bool enableHead = true;
    public float multiplier = 10f;
    public float landmarkScale = 1f;
    public float maxSpeed = 50f;
    public int samplesForPose = 1;
    public int port = 5005;

    [Header("Line Width Settings")]
    public float startWidthValue = 0.1f;
    public float endWidthValue = 0.1f;
    public float widthMultiplierValue = 1.0f;
    private Thread receiveThread;
    public Body body;
    
    // ★ BodyDistanceCalculatorへの参照を復活させる
    private BodyDistanceCalculator bodyDistanceCalculator;

    const int LANDMARK_COUNT = 23;
    const int LINES_COUNT = 11;

    // (GetNormal, AccumulatedBuffer, Bodyクラスの定義は変更ないので省略...）
    // (そのままペーストするため、以下のコードには含めてあります)

    private Vector3 GetNormal(Vector3 p1, Vector3 p2, Vector3 p3)
    {
        Vector3 u = p2 - p1;
        Vector3 v = p3 - p1;
        Vector3 n = new Vector3((u.y * v.z - u.z * v.y), (u.z * v.x - u.x * v.z), (u.x * v.y - u.y * v.x));
        float nl = Mathf.Sqrt(n[0] * n[0] + n[1] * n[1] + n[2] * n[2]);
        return new Vector3(n[0] / nl, n[1] / nl, n[2] / nl);
    }

    public struct AccumulatedBuffer
    {
        public Vector3 value;
        public int accumulatedValuesCount;
        public AccumulatedBuffer(Vector3 v,int ac)
        {
            value = v;
            accumulatedValuesCount = ac;
        }
    }

    public class Body 
    {
        public Transform parent;
        public AccumulatedBuffer[] positionsBuffer = new AccumulatedBuffer[LANDMARK_COUNT];
        public Vector3[] localPositionTargets = new Vector3[LANDMARK_COUNT];
        public GameObject[] instances = new GameObject[LANDMARK_COUNT];
        public LineRenderer[] lines = new LineRenderer[LINES_COUNT];
        public GameObject head;
        public bool active;
        public bool setCalibration = false;
        public Vector3 calibrationOffset;
        public Vector3 virtualHeadPosition;
        private IPEndPoint anyIP;

        public Body(Transform parent, GameObject linePrefab, float s, GameObject headPrefab)
        {
            this.parent = parent;
            for (int i = 0; i < instances.Length; ++i)
            {
                if (i == 9 || i == 10 || i == 11 || i == 12 || i == 19 || i == 20 || i == 21 || i == 22)
                {
                    for (int j = 0; j < parent.childCount; j++)
                    {
                        GameObject child = parent.GetChild(j).gameObject;
                        if (child.name == ((Landmark)i).ToString())
                        {
                            instances[i] = child;
                            instances[i].transform.localScale = Vector3.one * 0.5f;
                            break;
                        }
                    }
                }
                else
                {
                    for (int j = 0; j < parent.childCount; j++)
                    {
                        GameObject child = parent.GetChild(j).gameObject;
                        if (child.name == ((Landmark)i).ToString())
                        {
                            instances[i] = child;
                            instances[i].transform.localScale = Vector3.one * s;
                            break;
                        }
                    }
                }
                instances[i].transform.parent = parent;
                instances[i].name = ((Landmark)i).ToString();

                if (headPrefab && i >= 0 && i <= 2)
                {
                    instances[i].transform.localScale = Vector3.one * 0f;
                }
            }
            for (int i = 0; i < lines.Length; ++i)
            {
                lines[i] = Instantiate(linePrefab).GetComponent<LineRenderer>();
                lines[i].transform.parent = parent;
            }

            if (headPrefab)
            {
                head = headPrefab;
            }
        }
        public void UpdateLines()
        {
            Vector3 centershoulder = (Position((Landmark)3) + Position((Landmark)4)) / 2f;
            lines[0].positionCount = 4;
            lines[0].SetPosition(0, Position((Landmark)21));
            lines[0].SetPosition(1, Position((Landmark)19));
            lines[0].SetPosition(2, Position((Landmark)17));
            lines[0].SetPosition(3, Position((Landmark)21));
            lines[1].positionCount = 4;
            lines[1].SetPosition(0, Position((Landmark)22));
            lines[1].SetPosition(1, Position((Landmark)20));
            lines[1].SetPosition(2, Position((Landmark)18));
            lines[1].SetPosition(3, Position((Landmark)22));
            lines[2].positionCount = 4;
            lines[2].SetPosition(0, Position((Landmark)17));
            lines[2].SetPosition(1, Position((Landmark)15));
            lines[2].SetPosition(2, Position((Landmark)13));
            lines[2].SetPosition(3, Position((Landmark)14));
            lines[3].positionCount = 3;
            lines[3].SetPosition(0, Position((Landmark)18));
            lines[3].SetPosition(1, Position((Landmark)16));
            lines[3].SetPosition(2, Position((Landmark)14));
            lines[4].positionCount = 3;
            lines[4].SetPosition(0, Position((Landmark)13));
            lines[4].SetPosition(1, Position((Landmark)3));
            lines[4].SetPosition(2, centershoulder);
            lines[5].positionCount = 3;
            lines[5].SetPosition(0, Position((Landmark)14));
            lines[5].SetPosition(1, Position((Landmark)4));
            lines[5].SetPosition(2, centershoulder);
            lines[6].positionCount = 3;
            lines[6].SetPosition(0, Position((Landmark)3));
            lines[6].SetPosition(1, Position((Landmark)5));
            lines[6].SetPosition(2, Position((Landmark)7));
            lines[7].positionCount = 3;
            lines[7].SetPosition(0, Position((Landmark)4));
            lines[7].SetPosition(1, Position((Landmark)6));
            lines[7].SetPosition(2, Position((Landmark)8));
            lines[8].positionCount = 4;
            lines[8].SetPosition(0, Position((Landmark)7));
            lines[8].SetPosition(1, Position((Landmark)9));
            lines[8].SetPosition(2, Position((Landmark)11));
            lines[8].SetPosition(3, Position((Landmark)7));
            lines[9].positionCount = 4;
            lines[9].SetPosition(0, Position((Landmark)8));
            lines[9].SetPosition(1, Position((Landmark)10));
            lines[9].SetPosition(2, Position((Landmark)12));
            lines[9].SetPosition(3, Position((Landmark)8));
            if (!head)
            {
                lines[10].positionCount = 3;
                lines[10].SetPosition(0, Position((Landmark)8));
                lines[10].SetPosition(2, Position((Landmark)0));
                lines[10].SetPosition(4, Position((Landmark)7));
            }
        }
        public void Calibrate()
        {
            Debug.Log(localPositionTargets.Length);
            Vector3 centre = (localPositionTargets[13] + localPositionTargets[14]) / 2f;
            calibrationOffset = -centre;
            setCalibration = true;
        }

        public float GetAngle(Landmark referenceFrom, Landmark referenceTo, Landmark from, Landmark to)
        {
            Vector3 reference = (instances[(int)referenceTo].transform.position - instances[(int)referenceFrom].transform.position).normalized;
            Vector3 direction = (instances[(int)to].transform.position - instances[(int)from].transform.position).normalized;
            return Vector3.SignedAngle(reference, direction, Vector3.Cross(reference, direction));
        }
        public float Distance(Landmark from,Landmark to)
        {
            return (instances[(int)from].transform.position - instances[(int)to].transform.position).magnitude;
        }
        public Vector3 LocalPosition(Landmark Mark)
        {
            return instances[(int)Mark].transform.localPosition;
        }
        public Vector3 Position(Landmark Mark)
        {
            return instances[(int)Mark].transform.position;
        }
    }
    
    void ApplyLineWidthSettings(LineRenderer lineRenderer)
    {
        if (lineRenderer != null)
        {
            lineRenderer.startWidth = startWidthValue;
            lineRenderer.endWidth = endWidthValue;
            lineRenderer.widthMultiplier = widthMultiplierValue;
        }
    }

    private void Start()
    {
        System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.InvariantCulture;

        body = new Body(parent, linePrefab, landmarkScale, enableHead ? headPrefab : null);
        
        foreach (var line in body.lines)
        {
            ApplyLineWidthSettings(line);
        }

        // ★ BodyDistanceCalculatorとの連携を復活させる
        bodyDistanceCalculator = FindObjectOfType<BodyDistanceCalculator>();
        if (bodyDistanceCalculator == null)
        {
            Debug.LogError("BodyDistanceCalculator script not found in the scene! Please add it to a GameObject and assign the Main Camera.");
        }
        else
        {
            // BodyDistanceCalculator に、距離計算の対象となるオブジェクトを渡す
            bodyDistanceCalculator.bodySpheres = body.instances;
            bodyDistanceCalculator.bodyLines = body.lines;
        }

        Thread t = new Thread(new ThreadStart(Run));
        t.Start();
    }
    
    // (Update, UpdateBody, Run, OnDisableメソッドは変更ないので省略...)
    // (そのままペーストするため、以下のコードには含めてあります)
    private void Update()
    {
        UpdateBody(body);
    }
    public void UpdateBody(Body b)
    {
        //if (b.active == false) return;

        for (int i = 0; i < LANDMARK_COUNT; ++i)
        {
            if (b.positionsBuffer[i].accumulatedValuesCount < samplesForPose)
                continue;
            b.localPositionTargets[i] = b.positionsBuffer[i].value / (float)b.positionsBuffer[i].accumulatedValuesCount * multiplier;
            b.positionsBuffer[i] = new AccumulatedBuffer(Vector3.zero,0);
        }

        if (!b.setCalibration)
        {
            print("Set Calibration Data");
            b.Calibrate();

            if(FindObjectOfType<CameraController>())
                FindObjectOfType<CameraController>().Calibrate(b.instances[(int)Landmark.NOSE].transform);
        }

        for (int i = 0; i < LANDMARK_COUNT; ++i)
        {
            b.instances[i].transform.localPosition=Vector3.MoveTowards(b.instances[i].transform.localPosition, b.localPositionTargets[i]+b.calibrationOffset, Time.deltaTime * maxSpeed);
        }
        b.UpdateLines();

        b.virtualHeadPosition = (b.Position(Landmark.RIGHT_EAR) + b.Position(Landmark.LEFT_EAR)) / 2f;

        if (b.head)
        {
            b.head.transform.position = b.virtualHeadPosition+Vector3.up* .5f;
            Vector3 n1 = Vector3.Scale(new Vector3(.1f, 1f, .1f), GetNormal(b.Position((Landmark)0), b.Position((Landmark)8), b.Position((Landmark)7))).normalized;
            Vector3 n2 = Vector3.Scale(new Vector3(1f, .1f, 1f), GetNormal(b.Position((Landmark)0), b.Position((Landmark)4), b.Position((Landmark)1))).normalized;
            b.head.transform.rotation = Quaternion.LookRotation(-n2, n1);
        }
    }

    private void Run()
    {
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        
        
    }


}