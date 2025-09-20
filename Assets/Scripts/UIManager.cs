// UIManager.cs
using UnityEngine;
using UnityEngine.UI; // UI要素を使用するために必要

public class UIManager : MonoBehaviour
{
    [Header("UI要素")]
    [Tooltip("すべての情報を表示するための単一のTextコンポーネント")]
    public Text infoText; // <-- Textコンポーネントを一つに集約

    // 各情報を保持するプライベート変数
    private int currentSectionNum;
    private int currentFrameNum;
    private Vector3 currentCameraTransform;
    private float currentNoiseIntensity;
    private float currentNoiseSpeed;

    void Start()
    {
        // TextコンポーネントがInspectorでアサインされているか確認
        if (infoText == null)
        {
            Debug.LogWarning("UIManager: infoTextがアサインされていません。Inspectorで設定してください。");
        }

        // 初期値を設定
        currentSectionNum = 0;
        currentFrameNum = 0;
        currentCameraTransform = Vector3.zero;
        currentNoiseIntensity = 0f;
        currentNoiseSpeed = 0f;

        // 初期表示を更新
        UpdateAllUI();
    }

    /// <summary>
    /// 受信したセクション数を更新します。
    /// </summary>
    public void UpdateSectionNumUI(int num)
    {
        currentSectionNum = num;
        UpdateAllUI();
    }

    /// <summary>
    /// 受信したフレーム数を更新します。
    /// </summary>
    public void UpdateFrameNumUI(int num)
    {
        currentFrameNum = num;
        UpdateAllUI();
    }

    /// <summary>
    /// 受信したカメラ座標を更新します。
    /// </summary>
    public void UpdateCameraTransformUI(Vector3 transform)
    {
        currentCameraTransform = transform;
        UpdateAllUI();
    }

    /// <summary>
    /// 受信したノイズ強度を更新します。
    /// </summary>
    public void UpdateNoiseIntensityUI(float intensity)
    {
        currentNoiseIntensity = intensity;
        UpdateAllUI();
    }

    /// <summary>
    /// 受信したノイズ速度を更新します。
    /// </summary>
    public void UpdateNoiseSpeedUI(float speed)
    {
        currentNoiseSpeed = speed;
        UpdateAllUI();
    }

    /// <summary>
    /// 保持している全ての情報をまとめてUIに表示します。
    /// </summary>
    private void UpdateAllUI()
    {
        if (infoText != null)
        {
            string displayText = 
                $"Section: {currentSectionNum}\n" +
                $"Frame: {currentFrameNum}\n" +
                $"Cam Pos: X:{currentCameraTransform.x:F2} Y:{currentCameraTransform.y:F2} Z:{currentCameraTransform.z:F2}\n" +
                $"Noise I: {currentNoiseIntensity:F3}\n" +
                $"Noise S: {currentNoiseSpeed:E2}"; // E2は指数表記 (例: 1.00E-05)

            infoText.text = displayText;
        }
    }
}