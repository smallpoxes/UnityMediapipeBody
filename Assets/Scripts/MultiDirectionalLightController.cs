using UnityEngine;
using System.Collections.Generic; // Listを使うために必要

public class MultiDirectionalLightController : MonoBehaviour
{
    public bool IntensitySwitch;
    public bool RotationSwitch;
    public bool RotationVariationSwitch;
    public bool HysteresisGateSwitch;


    public float onThreshold = 0.6f;//if using histeresis
    public float offThreshold = 0.55f;//if using histeresis
    public float cooldown = 0.8f;   //if using histeresis
    public int pBase = 0;                 // 0..5 の順列ID
    float cooldownTimer = 0f;
    public bool gate;
    public Vector3 targetAxis;
    public Vector3 baseAxis = Vector3.up; // 基本軸
    public Vector3 axisSmoothed;


    [Range(0, 2)] public int offset = 0; // 0,1,2 → p = (pBase + offset) % 6

    [Header("Orbit")]
    public float baseSpeed = 30f;     // deg/sec
    public float smoothTime = 0.10f;
    [Header("Direction")]
    public bool reverseToggle = false;
    [Range(0, 1)] public float dirFeature = 0.5f; // 0..1 → -1..1
    float speedSmoothed;



    public GameObject TcpObject; // TcpCommunicatorを持つGameObjectの参照
    private TcpCommunicator tcpCommunicator; // TcpCommunicatorの参照
    public int featureIndex = 0; // インスペクターから設定可能な整数値
    public float intensitySeed;
    public AnimationCurve intensityCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.3f, 0.8f),  // Steep rise
        new Keyframe(0.7f, 0.9f),  // Gentle middle
        new Keyframe(1f, 1f)       // Steep rise again
    );
    public float minIntensity = 0.5f; // 最小強度
    public float maxIntensity = 3.0f; // 最大強度
    public float rotationSeed = 10.0f;
    public float dir = 1.0f; // 回転方向を示す変数



    public float minRotationSpeed = 0.0f; // 最小回転速度
    public float maxRotationSpeed = 50.0f; // 最大回転速度

    [Tooltip("ライトコンポーネントを直接割り当てる（任意）")]
    public List<Light> directionalLights = new List<Light>(); // Inspectorで直接D-Lightを割り当てるリスト
    [Range(0.0f, 360.0f)]
    public float redrotate;
    [Range(0.0f, 360.0f)]
    public float yellowrotate;
    [Range(0.0f, 360.0f)]
    public float bluerotate;
    private Transform[] lightTransforms; // 各Directional LightのTransformを保持する配列
    public float intensity;
    public float rotationSpeed;
    public float featureValue;


    void Awake()
    {
        // もしInspectorで直接リストが設定されていなければ、子から自動取得を試みる
        if (directionalLights.Count == 0)
        {
            // GetComponentsInChildrenで、このオブジェクト自身とすべての子孫からLightコンポーネントを取得


            // 例2: 全ての子孫から取得（自身を含む場合もあるので注意）
            // directionalLights.AddRange(GetComponentsInChildren<Light>(false)); // falseで非アクティブな子も含む
            // directionalLights.RemoveAll(l => l.type != LightType.Directional); // Directional Light以外を除外

            if (directionalLights.Count == 0)
            {
                Debug.LogWarning("子のDirectional Lightが見つかりません。Inspectorで設定するか、子として配置してください。", this);
                enabled = false; // スクリプトを無効化
                return;
            }

        }

        // 各ライトのTransformをキャッシュ
        lightTransforms = new Transform[directionalLights.Count];
        for (int i = 0; i < directionalLights.Count; i++)
        {
            lightTransforms[i] = directionalLights[i].transform;
        }
    }

    void Start()
    {
        if (directionalLights.Count == 0)
        {
            Debug.LogError("Directional Lights are not assigned. Please assign them in the Inspector or ensure they are children of this GameObject.", this);
            enabled = false; // スクリプトを無効化
            return;
        }

        tcpCommunicator = TcpObject.GetComponent<TcpCommunicator>();
        Resample();
        //InitializeLightPositions();

    }

    void Update()
    {
        
        if (tcpCommunicator.features != null && tcpCommunicator.features.Length > featureIndex)
        {
            featureValue = tcpCommunicator.features[featureIndex];
            float curveValue = intensityCurve.Evaluate(featureValue);

            rotationSpeed = RotationSwitch ? rotationSeed * Mathf.Lerp(minRotationSpeed, maxRotationSpeed, curveValue) : rotationSpeed;

            if (!RotationVariationSwitch)
            {
                MDLCmethods.RotateLights(ref redrotate, ref yellowrotate, ref bluerotate, lightTransforms, rotationSpeed);
            }
            else
            {
                MDLCmethods.ProcessVariousRotation(RotationVariationSwitch, lightTransforms,
                                                    baseAxis, pBase, dirFeature,
                                                    smoothTime, rotationSpeed, featureValue, ref axisSmoothed);
            }

            //Debug.Log($"Pre-Intensity: {Mathf.Lerp(minIntensity, maxIntensity, curveValue)}, current: {curveValue}");
            ApplyIntensity(curveValue);

        }

        if (HysteresisGateSwitch)
        {
            HysteresisGate();
        }
        else
        {
            SimpleGate();
        }
    }

    void ApplyIntensity(float curveValue)
    {
        intensity = IntensitySwitch ? intensitySeed * Mathf.Lerp(minIntensity, maxIntensity, curveValue) : intensity;
        foreach (var dl in directionalLights)
        {
            if (dl != null)
            {
                dl.intensity = intensity;
            }
        }
    }

    void HysteresisGate()
    {
        cooldownTimer -= Time.deltaTime;
        // ヒステリシス・ゲート
        gate = gate ? (featureValue > offThreshold) : (featureValue > onThreshold);
        if (gate && cooldownTimer <= 0f) { Resample(); cooldownTimer = cooldown; }

    }
    void SimpleGate()
    {
        gate = (featureValue > offThreshold);
        if (gate) { Resample(); }
    }

    void Resample()
    {
        // ランダムな単位ベクトル（0ベクトル回避＆正規化）
        baseAxis = Random.onUnitSphere.normalized;
        // 0..5 の順列IDを更新
        pBase = Random.Range(0, 6);
    }





    // 例えば、外部からライトの特定の角度を設定したい場合
    public void SetLightRotation(int lightIndex, Quaternion newRotation)
    {
        if (lightTransforms != null && lightIndex >= 0 && lightIndex < lightTransforms.Length)
        {
            lightTransforms[lightIndex].localRotation = newRotation;
        }
    }

    // 各ライトの色をランダムに変える例
    [ContextMenu("Randomize Light Colors")] // Inspectorで右クリックメニューに追加
    void RandomizeLightColors()
    {
        foreach (Light dl in directionalLights)
        {
            if (dl != null)
            {
                dl.color = new Color(Random.value, Random.value, Random.value, 1.0f);
            }
        }
    }
    
    
}