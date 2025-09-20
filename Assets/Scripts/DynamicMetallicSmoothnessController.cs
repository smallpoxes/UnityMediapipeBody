using UnityEngine;

public class DynamicMetallicSmoothnessController : MonoBehaviour
{
    public Renderer targetRenderer; // マテリアルがアタッチされているRendererコンポーネント

    [Header("Shader Graph Property Names")]
    [Tooltip("Shader Graphで作成した_Metallicプロパティの名前")]
    public string metallicPropertyName = "_Metallic"; 
    [Tooltip("Shader Graphで作成した_Smoothnessプロパティの名前")]
    public string smoothnessPropertyName = "_Smoothness";
    [Tooltip("Shader Graphで作成したカスタムタイムプロパティの名前（オプション）")]
    public string customTimePropertyName = "_CustomTime"; 

    [Header("Animation Settings")]
    [Tooltip("メタリック値の揺れ幅")]
    [Range(0f, 1f)]
    public float metallicAmplitude = 0.5f; 
    [Tooltip("メタリック値の揺れ速度")]
    public float metallicFrequency = 1.0f;

    [Tooltip("滑らかさ値の揺れ幅")]
    [Range(0f, 1f)]
    public float smoothnessAmplitude = 0.5f;
    [Tooltip("滑らかさ値の揺れ速度")]
    public float smoothnessFrequency = 1.0f;

    [Tooltip("時間オフセット (異なるライト/オブジェクトで同期しない動きのため)")]
    public float timeOffset = 0f;

    // デバッグログ出力の頻度を制御するための変数
    [Tooltip("デバッグログを出力する間隔 (秒)。0で毎フレーム。")]
    public float logInterval = 1.0f;
    private float nextLogTime = 0f;


    private Material _targetMaterial; 

    void Start()
    {
        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<Renderer>();
        }
        if (targetRenderer != null)
        {
            _targetMaterial = targetRenderer.material; 
        }
        else
        {
            Debug.LogError("ターゲットRendererが見つかりません。", this);
            enabled = false;
        }

        // 初回実行時にデバッグログを出力
        LogShaderProperties();
    }

    void Update()
    {
        if (_targetMaterial == null) return;

        float currentTime = Time.time + timeOffset;

        float animatedMetallic = (Mathf.Sin(currentTime * metallicFrequency) * metallicAmplitude + 1f) / 2f;
        float animatedSmoothness = (Mathf.Sin(currentTime * smoothnessFrequency + Mathf.PI / 2f) * smoothnessAmplitude + 1f) / 2f; 

        _targetMaterial.SetFloat(metallicPropertyName, animatedMetallic);
        _targetMaterial.SetFloat(smoothnessPropertyName, animatedSmoothness);
        if (!string.IsNullOrEmpty(customTimePropertyName))
        {
            _targetMaterial.SetFloat(customTimePropertyName, currentTime);
        }

        // 指定された間隔でデバッグログを出力
        if (Time.time >= nextLogTime)
        {
            LogShaderProperties();
            nextLogTime = Time.time + logInterval;
        }
    }

    // Shader Graphのプロパティを読み取ってデバッグログに出力する関数
    public void LogShaderProperties()
    {
        if (_targetMaterial == null)
        {
            Debug.LogWarning("マテリアルが設定されていません。");
            return;
        }

        // Metallicプロパティの値を読み取り
        if (_targetMaterial.HasProperty(metallicPropertyName))
        {
            float currentMetallic = _targetMaterial.GetFloat(metallicPropertyName);
            //Debug.Log($"[ShaderVarDebug] {metallicPropertyName}: {currentMetallic:F4}");
        }
        else
        {
            //Debug.LogWarning($"マテリアルにプロパティ '{metallicPropertyName}' が見つかりません。名前が正しいか確認してください。");
        }

        // Smoothnessプロパティの値を読み取り
        if (_targetMaterial.HasProperty(smoothnessPropertyName))
        {
            float currentSmoothness = _targetMaterial.GetFloat(smoothnessPropertyName);
            Debug.Log($"[ShaderVarDebug] {smoothnessPropertyName}: {currentSmoothness:F4}");
        }
        else
        {
            //Debug.LogWarning($"マテリアルにプロパティ '{smoothnessPropertyName}' が見つかりません。名前が正しいか確認してください。");
        }

        // CustomTimeプロパティの値を読み取り（オプション）
        if (!string.IsNullOrEmpty(customTimePropertyName) && _targetMaterial.HasProperty(customTimePropertyName))
        {
            float currentCustomTime = _targetMaterial.GetFloat(customTimePropertyName);
            //Debug.Log($"[ShaderVarDebug] {customTimePropertyName}: {currentCustomTime:F4}");
        }
    }

    void OnDestroy()
    {
        if (_targetMaterial != null)
        {
            Destroy(_targetMaterial);
        }
    }
}