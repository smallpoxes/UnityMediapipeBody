using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChangeLineColrEffect : MonoBehaviour
{
    public GameObject TcpObject; // TcpCommunicatorを持つGameObjectの参照
    private TcpCommunicator tcpCommunicator; // TcpCommunicatorの参照
    public int featureIndex = 0; // 特徴量のインデックス
    public float threshold = 0.5f; // 特徴量の閾値
    public float scaleOffset = 1.0f; // スケールのオフセット
    public float valueScaller = 1.0f; // 特徴量に基づくスケール調整の係数
    public Material lineMaterial; // Lineが参照するマテリアル
    public Vector3 hsv; // HSV値を格納するVector3
    public Color originalColor; // 元の色
    private Vector3 originalHSV; // 元のHSV値
    private LineRenderer[] lineRenderers;
    private bool lineInitialized = false;
    private float originalWidth;
    public GameObject headObject;
    public float eyeScale = 0.25f;
    private bool lineWidthChanged = false;
    private bool eyeScaleChanged = false;


    // Start is called before the first frame update
    void Start()
    {
        hsv = new Vector3(1f, 1f, 1f);
        tcpCommunicator = TcpObject.GetComponent<TcpCommunicator>();
        originalColor = new Color(255, 0, 208); // 最初のLineRendererの色を取得
        Color.RGBToHSV(originalColor, out float h, out float s, out float v); // 元の色をHSVに変換
        originalHSV = new Vector3(h, s, v); // HSV値をVector3に格納
        foreach (Transform child in headObject.transform)
        {
            Debug.Log(child.name + " child.localScale: " + child.localScale);
        }
    }

    // Update is called once per frame
    void Update()
    {

        if (!lineInitialized && transform.childCount > 0)
        {
            List<LineRenderer> lines = new List<LineRenderer>();
            for (int i = 0; i < transform.GetChild(0).childCount; i++)
            {
                if (transform.GetChild(0).GetChild(i).name == "HandLine(Clone)")
                {
                    var lr = transform.GetChild(0).GetChild(i).GetComponent<LineRenderer>();
                    originalWidth = lr.startWidth;
                    Debug.Log("originalWidth: " + originalWidth);
                    if (lr != null) lines.Add(lr);
                    Debug.Log("originalWidth1.2: " + originalWidth);
                }    // nullチェック
                Debug.Log("originalWidth1.5: " + originalWidth);

            }
            lineRenderers = lines.ToArray();
            lineInitialized = true;
            lineMaterial.DisableKeyword("_EMISSION");
        }
        else if (lineInitialized)
        {
            Debug.Log("originalWidth2: " + originalWidth);
            //ここがelseであれば、色は元に戻す必要がある
            if (tcpCommunicator.features != null && tcpCommunicator.features.Length > featureIndex && tcpCommunicator.features[featureIndex] <= threshold)
            {
                Debug.Log("originalWidth3: " + originalWidth);
                // 特徴量の値に基づいて色を変更
                hsv.x += tcpCommunicator.features[featureIndex] * valueScaller;
                if (hsv.x > 1f) hsv.x = 0f; // 色相を0-1の範囲に制限
                Color newColor = Color.HSVToRGB(hsv.x, originalHSV.y, originalHSV.z); // HSVからRGBに変換

                // 通常の色設定
                lineMaterial.color = newColor;

                // 発光設定
                lineMaterial.EnableKeyword("_EMISSION");
                lineMaterial.SetColor("_EmissionColor", newColor * 2f); // 発光色を設定（明るくするため2倍）

                foreach (LineRenderer lr in lineRenderers)
                {
                    lr.startWidth = originalWidth * (30 * tcpCommunicator.features[featureIndex]);
                    lr.endWidth = originalWidth * (30 * tcpCommunicator.features[featureIndex]);
                }
                lineWidthChanged = true;
                foreach (Transform child in headObject.transform)
                {
                    child.localScale = new Vector3(eyeScale * (10 * tcpCommunicator.features[featureIndex]),
                                            eyeScale * (10 * tcpCommunicator.features[featureIndex]),
                                            eyeScale * (10 * tcpCommunicator.features[featureIndex]));
                }
                eyeScaleChanged = true;
            }
            else
            {
                Debug.Log("originalWidth4: " + originalWidth);
                if (lineMaterial.color != originalColor)
                {
                    lineMaterial.color = originalColor; // 元の色に戻す

                    // 発光を無効化
                    lineMaterial.DisableKeyword("_EMISSION");
                    lineMaterial.SetColor("_EmissionColor", Color.black);
                }
                if (lineWidthChanged)
                {
                    foreach (LineRenderer lr in lineRenderers)
                    {
                        lr.startWidth = originalWidth;
                        lr.endWidth = originalWidth;
                    }
                    lineWidthChanged = false;
                }
                if (eyeScaleChanged)
                {
                    foreach (Transform child in headObject.transform)
                    {
                        child.localScale = new Vector3(eyeScale, eyeScale, eyeScale);
                    }
                    eyeScaleChanged = false;
                }   
                
            }
        }

        
    }
}

//閾値を設定して、それ以上なら、色が変わっていく。色の形式はHSVにする
//色を変えるときは0=>1へ変化？ゆっくりだったり、早かったり、ウェイトで計算しても良いかも
//速度については、thresholdを超えた段階での強度を取るか。強度変数は、そのままfeaturesを使う
//浮動小数型なので、何段階で変化するのかを決めなくては。255段階にするか。1/255。分子をintにして、
//時間経過で分子に1を加算していく。いや、分子をfeaturesにして、2倍するとか。そんな感じにしてみるか
//