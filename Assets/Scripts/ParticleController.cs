

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CartoonFX;

public class ParticleController : MonoBehaviour
{
    public Transform ParentObject; // PipeServer.csのparentを割り当てる
    public Transform TcpObject;
    private TcpCommunicator tcpCommunicator; // インスペクターで割り当てる
    [Tooltip("特徴量のインデックス。center_x: 0, center_y: 1, center_z: 2, height: 3, arm_span: 4, torso_area: 5, shoulder_tilt: 6, hip_tilt: 7, stance_width: 8, arm_angle_L: 9, arm_angle_R: 10, leg_angle_L: 11, leg_angle_R: 12, symmetry: 13, radius: 14")]
    public int featureIndex = 0; // インスペクターから設定可能な整数値
    public float threshold = 0.5f; // 特徴量の閾値
    public float scaleOffset = 1.0f; // スケールのオフセット
    public float valueScaller = 1.0f; // 特徴量に基づくスケール調整の係数
    public float alphascaler = 1.0f; // アルファ値のスケール調整の係数
    private ParticleSystemRenderer particleRenderer;
    private ParticleSystem particleSystem;
    private Material particleMaterial;
    public Color currentColor;
    private float[] currentValue = new float[2]; // [0]は現在値, [1]は前回値
    private Light attachedLight;
    public float fadeSpeed = 0.1f; // 1秒で0.1ずつ減少
    private bool howfade = true; // trueなら減少、falseなら増加
    private CFXR_Effect fxrEffect;
    const string oneshottag = "oneshot";
    const string neverendtag = "neverend";

    void Start()
    {
        currentValue = new float[2] { 0f, 0f }; // 初期化
        tcpCommunicator = TcpObject.GetComponent<TcpCommunicator>();
        Debug.Log(this.name);
        particleRenderer = GetComponent<ParticleSystemRenderer>();
        particleSystem = GetComponent<ParticleSystem>();
        particleMaterial = particleRenderer.material;
        currentColor.a = 1f; // 初期アルファ値を1に設定
        fxrEffect = GetComponent<CFXR_Effect>();
        Debug.Log(this.tag + "oneshot:" + (this.tag == oneshottag) + " neverend:" +
        (this.tag == neverendtag) + "it is: " + this.name + " and .animatedLights is: " + (fxrEffect.animatedLights != null));
    }

    void Update()
    {
        this.transform.position = ParentObject.position;
        //Debug.Log("features :" + (tcpCommunicator.features != null ? string.Join(", ", tcpCommunicator.features) : "null"));
        if (this.tag == oneshottag)
        {
            currentValue[1] = currentValue[0]; // 前回値を更新
            currentValue[0] = tcpCommunicator.features != null && tcpCommunicator.features.Length > featureIndex
                ? Mathf.Max((tcpCommunicator.features[featureIndex]-threshold)/(1-threshold), 0f)
                : 0f; // 特徴量がnullまたはインデックスが範囲外の場合は0に設定
            if (currentValue[0] > threshold && currentValue[1] <= threshold)
            {
                // 特徴量の値が閾値を超えた場合にパーティクルシステムを再生
                Debug.Log("Feature value exceeded threshold, playing particle system.");
                particleSystem.Play();
            }
        }

        else if (this.tag == neverendtag)
        {

            featuresAlpha();
            //Debug.Log("neverendtag is true, currentColor.a: " + currentColor.a);
        }


    }

    void featuresAlpha()
    {
       currentColor = tcpCommunicator.features != null && tcpCommunicator.features.Length > featureIndex
                ? new Color(1f, 1f, 1f, Mathf.Max((tcpCommunicator.features[featureIndex]-threshold)/(1-threshold)*alphascaler, 0f))
                : new Color(1f, 1f, 1f, 1f); // 特徴量がnullまたはインデックスが範囲外の場合は透明にする
            particleMaterial.SetColor("_TintColor", currentColor);

        this.transform.localScale = new Vector3(
            tcpCommunicator.features != null && tcpCommunicator.features.Length > featureIndex
                ? Mathf.Max((tcpCommunicator.features[featureIndex]-threshold)/(1-threshold), 0f) * valueScaller // スケールを特徴量に基づいて調整
                : scaleOffset, // 特徴量がnullまたはインデックスが範囲外の場合はデフォルトスケール
            tcpCommunicator.features != null && tcpCommunicator.features.Length > featureIndex
                ? Mathf.Max((tcpCommunicator.features[featureIndex]-threshold)/(1-threshold), 0f) * valueScaller
                : scaleOffset,
            tcpCommunicator.features != null && tcpCommunicator.features.Length > featureIndex
                ? Mathf.Max((tcpCommunicator.features[featureIndex]-threshold)/(1-threshold), 0f) * valueScaller
                : scaleOffset
        );

            if (fxrEffect != null && fxrEffect.animatedLights != null)
        {
            foreach (var animLight in fxrEffect.animatedLights)
            {
                if (animLight != null && animLight.light != null)
                {
                    // 例：アルファ値と連動してintensityを変える
                    animLight.intensityMultiplier = currentColor.a;

                    // 例：fadeIn/fadeOutを手動で切り替えたい場合
                    // animLight.fadeIn = true/false;
                    // animLight.fadeOut = true/false;
                }
            }
        }  
    }
    void fadeExample()
    {
        Debug.Log("now alpha: " + currentColor.a);
            // アルファ値を徐々に減少
            if (currentColor.a >= 1f)
            {
                howfade = true; // アルファ値が1以上なら減少
            }
            else if (currentColor.a <= 0f)
            {
                howfade = false; // アルファ値が0以下なら増加
            }

            if (howfade)
            {
                currentColor.a = Mathf.Max(0f, currentColor.a - fadeSpeed * Time.deltaTime);
            }
            else
            {
                currentColor.a = Mathf.Min(1f, currentColor.a + fadeSpeed * Time.deltaTime);
            }

            particleMaterial.SetColor("_TintColor", currentColor);

            if (fxrEffect != null && fxrEffect.animatedLights != null)
            {
                foreach (var animLight in fxrEffect.animatedLights)
                {
                    if (animLight != null && animLight.light != null)
                    {
                        // 例：アルファ値と連動してintensityを変える
                        animLight.intensityMultiplier = currentColor.a;

                        // 例：fadeIn/fadeOutを手動で切り替えたい場合
                        // animLight.fadeIn = true/false;
                        // animLight.fadeOut = true/false;
                    }
                }
            }
    }
}

//継続しつつ透明度が変わるようなパターンと、単発でエフェクトが出るパターンを実装したい。
//bouncing bubbleについてはOK　単発もOk
//GrouingHDRを設定
//どの特徴量をどのエフェクトに当てるかを決めなくては。まずは特徴量の種類についても知らなくては
//どのエフェクトをどの部位に付与するかも決まっていない

// 特徴量の種類
//# 特徴名	内容（直感的な意味）
//# center[x,y,z]	重心位置（例：腰と頭の中間）
//# pose_height	身長に相当する縦の長さ
//# arm_span	両手の横幅（ポーズによる広がり）＝＞impactHDR
//# torso_area	胴体三角形の面積（開き・捻じれの表現）＝＞changeLineColor(これは、捻れると数値が下がるか？)
//# shoulder_tilt	両肩の高さ差（傾き）
//# hip_tilt	両腰の高さ差（傾き）
//# stance_width	両足のX方向距離（開きの広さ）
//# miniball_radius	全身の広がりの包絡球（空間的サイズ）＝＞growingbubble
//# symmetry_score	左右関節対称性スコア（非対称なら大きな値）　＝＞magic aura
//# arm_angle_L/R	肩→肘→手の角度（曲がり具合）
//# leg_angle_L/R	股→膝→足の角度（屈伸など）

// FEATURE_INDEX = {
//     "center_x": 0,
//     "center_y": 1,
//     "center_z": 2,
//     "height": 3,
//     "arm_span": 4,
//     "torso_area": 5,
//     "shoulder_tilt": 6,
//     "hip_tilt": 7,
//     "stance_width": 8,
//     "arm_angle_L": 9,
//     "arm_angle_R": 10,
//     "leg_angle_L": 11,
//     "leg_angle_R": 12,
//     "symmetry": 13,
//     "radius": 14
// }

//symmetryはbouncingbubbleを使ってみよう
//

//インスペクターから、整数値を指定。整数値に応じた特長量をToolTipsで表示