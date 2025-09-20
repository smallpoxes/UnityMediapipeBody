// PoseData.cs
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

[System.Serializable]
public class PoseData
{
    [JsonProperty("frameNum")]
    public int frameNum;
    [JsonProperty("section")] // <-- 新しく追加: Pythonから送信されるセクション数
    public int sectionNum;

    [JsonProperty("landmarks")]
    public List<List<float>> landmarks;

    [JsonProperty("param")]
    public float param;

    [JsonProperty("_NoiseIntensity")]
    public float noiseIntensity;

    [JsonProperty("_NoiseSpeed")]
    public float noiseSpeed;

    [JsonProperty("cameratransform")]
    public List<float> cameraTransform; // XYZ座標のみを受け取る

    [JsonProperty("cameraMode")]
    public int cameraMode;

    [JsonProperty("cameraKeySpeeds")]
    public List<float> cameraKeySpeeds;

    [JsonProperty("cameraMoveSpeedFactor")] // <-- 新しく追加: Pythonから送信される速度係数
    public float cameraMoveSpeedFactor;
    [JsonProperty("features")] // 特徴量を格納するプロパティ
    public List<float> features; // 特徴量を格納するリスト
}