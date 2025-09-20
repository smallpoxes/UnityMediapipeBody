// UnityFeedbackData.cs
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

// UnityからPythonへ送信するフィードバックデータの構造
[System.Serializable]
public class UnityFeedbackData
{
    [JsonProperty("frameNum")]
    public int frameNum; // 現在のフレーム番号

    [JsonProperty("cameraPosition")]
    public List<float> cameraPosition; // カメラのワールド座標 [x, y, z]

    [JsonProperty("targetPosition")]
    public List<float> targetPosition; // ターゲット（アバターの頭部など）のワールド座標 [x, y, z]

    [JsonProperty("cameraToTargetRelativePosition")]
    public List<float> cameraToTargetRelativePosition; // ターゲットから見たカメラの相対位置 (CameraPos - TargetPos) [x, y, z]

    [JsonProperty("cameraMode")]
    public int cameraMode; // 現在のカメラモード (0:キーバインド, 1:follow, 2:vector)

    [JsonProperty("message")]
    public string message; // オプションのメッセージ（デバッグ用など）

    // コンストラクタ
    public UnityFeedbackData(int frameNum, Vector3 camPos, Vector3 targetPos, int camMode, string msg = "")
    {
        this.frameNum = frameNum;
        this.cameraPosition = new List<float> { camPos.x, camPos.y, camPos.z };
        this.targetPosition = new List<float> { targetPos.x, targetPos.y, targetPos.z };

        Vector3 relativePos = camPos - targetPos; // カメラの位置 - ターゲットの位置
        this.cameraToTargetRelativePosition = new List<float> { relativePos.x, relativePos.y, relativePos.z };
        
        this.cameraMode = camMode;
        this.message = msg;
    }
}