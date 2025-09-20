using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChildrenInitializer : MonoBehaviour
{
    public int featureIndex; // 特徴量のインデックス
    public float threshold; // 特徴量の閾値
    public float scaleOffset = 1.0f; // スケールのオフセット
    public float valueScaller = 1.0f; // 特徴量に基づくスケール調整の係数
    // Start is called before the first frame update
    void Start()
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            ParticleController particleController = child.GetComponent<ParticleController>();
            if (particleController != null)
            {
                // 特徴量のインデックスと閾値を設定
                particleController.featureIndex = featureIndex;
                particleController.threshold = threshold;
                particleController.scaleOffset = scaleOffset;
                particleController.valueScaller = valueScaller; // 特徴量に基づくスケール調整の係数を設定

            }
            for (int j = 0; j < child.childCount; j++)
            {
                Transform grandChild = child.GetChild(j);
                ParticleController grandChildParticleController = grandChild.GetComponent<ParticleController>();
                if (grandChildParticleController != null)
                {
                    // 特徴量のインデックスと閾値を設定
                    grandChildParticleController.featureIndex = featureIndex;
                    grandChildParticleController.threshold = threshold;
                    grandChildParticleController.scaleOffset = scaleOffset;
                    grandChildParticleController.valueScaller = valueScaller; // 特徴量に基づくスケール調整の係数を設定
                }
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
