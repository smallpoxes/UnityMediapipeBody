using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using Newtonsoft.Json;
using UnityMainThreadDispatcher;

// TcpServerV2をリネームし、双方向通信を扱うCommunicatorとする
public class TcpCommunicatorV2 : MonoBehaviour
{
    public int port = 8080;
    public PipeServerV2 pipeServer; 
    public GameObject gridObject; 
    public GameObject cameraObject; 
    public UIManager uiManager; 
    
    // 他のスクリプトへの参照
    private CameraView cameraView; 

    // マテリアルインスタンス
    private Material gridMaterial; 

    // 受信した生のデータ（デバッグ用）
    public string receivedRawData;
    public float receivedParam;
    public Vector3[] receivedLandmarks = new Vector3[23];

    // 最新の受信データ（共有変数）
    private float _latestNoiseIntensity;
    private float _latestNoiseSpeed;
    private Vector3 _latestCameraTransform; 
    private int _latestCameraMode; 
    private List<float> _latestCameraKeySpeeds; 
    private float _latestCameraMoveSpeedFactor; 
    private int _latestSectionNum; 
    private int _latestFrameNum; 
    private bool _newDataAvailable = false; // 新しいデータが利用可能かを示すフラグ
    private readonly object _latestDataLock = new object(); // 共有変数保護のためのロックオブジェクト

    // TCPサーバー関連のスレッドとリスナー
    private TcpListener tcpListener;
    private Thread listenThread;

    void Start()
    {
        Debug.Log("TcpCommunicatorV2 Start called.");

        if (typeof(JsonConvert) == null)
        {
            Debug.LogError("Newtonsoft.Json (Json.NET) not found. Please import it into your Unity project.");
            return;
        }

        if (pipeServer == null)
        {
            pipeServer = FindObjectOfType<PipeServerV2>();
            if (pipeServer == null)
            {
                Debug.LogError("PipeServerV2 script not found in the scene! Please add it to a GameObject and assign it in the Inspector.");
                return;
            }
        }

        if (gridObject != null)
        {
            Renderer gridRenderer = gridObject.GetComponent<Renderer>();
            if (gridRenderer != null)
            {
                gridMaterial = gridRenderer.material;
                _latestNoiseIntensity = gridMaterial.GetFloat("_NoiseIntensity");
                _latestNoiseSpeed = gridMaterial.GetFloat("_NoiseSpeed");
                Debug.Log($"TcpCommunicatorV2: Initial NoiseIntensity: {_latestNoiseIntensity}, NoiseSpeed: {_latestNoiseSpeed}");
            }
            else
            {
                Debug.LogError("GridObjectにRendererコンポーネントが見つかりません。マテリアル設定不可。");
            }
        }
        else
        {
            Debug.LogError("GridObjectが設定されていません。マテリアル操作ができません。");
        }

        if (cameraObject != null)
        {
            cameraView = cameraObject.GetComponent<CameraView>();
            if (cameraView == null)
            {
                Debug.LogWarning("CameraObjectにCameraViewコンポーネントが見つかりません。カメラ制御が無効になります。");
            }
            _latestCameraTransform = cameraObject.transform.position;
            Debug.Log($"TcpCommunicatorV2: Initial CameraTransform: {_latestCameraTransform}");
        }
        else
        {
            _latestCameraTransform = Vector3.zero;
            Debug.LogWarning("CameraObjectが設定されていません。カメラの初期位置はVector3.zeroです。");
        }

        if (uiManager == null)
        {
            uiManager = FindObjectOfType<UIManager>();
            if (uiManager == null)
            {
                Debug.LogWarning("UIManager script not found in the scene! Please add it to a GameObject and assign it in the Inspector.");
            }
        }

        // Unityのフレームレートを30FPSに固定 (Pythonの送信FPSに合わせる)
        Application.targetFrameRate = 30; 

        // 最新データ保持変数の初期値設定
        _latestCameraMode = 0;
        _latestCameraKeySpeeds = new List<float>(new float[6]);
        _latestCameraMoveSpeedFactor = 0f; 
        _latestSectionNum = 0; 
        _latestFrameNum = 0; 

        // TCPリスナーを別のスレッドで開始
        listenThread = new Thread(new ThreadStart(ListenForClients));
        listenThread.IsBackground = true; 
        listenThread.Start();
        Debug.Log("TCP Server Started on port " + port);
    }

    void OnApplicationQuit()
    {
        Debug.Log("TcpCommunicatorV2 OnApplicationQuit called. Attempting to stop TCP Listener and Thread.");
        if (tcpListener != null)
        {
            tcpListener.Stop();
            Debug.Log("TCP Listener Stopped.");
        }
        if (listenThread != null && listenThread.IsAlive)
        {
            listenThread.Abort(); 
            Debug.Log("Listen Thread Aborted.");
        }
    }

    void Update()
    {
        // Debug.Log("TcpCommunicatorV2 Update called. _newDataAvailable: " + _newDataAvailable); 

        if (_newDataAvailable) 
        {
            Debug.Log("TcpCommunicatorV2 Update: _newDataAvailable is TRUE. Attempting to acquire lock.");

            float currentNoiseIntensity;
            float currentNoiseSpeed;
            Vector3 currentCameraTransform;
            int currentCameraMode;
            List<float> currentCameraKeySpeeds;
            float currentCameraMoveSpeedFactor;
            int currentSectionNum;
            int currentFrameNum;

            lock (_latestDataLock)
            {
                Debug.Log("TcpCommunicatorV2 Update: Lock acquired. Resetting _newDataAvailable.");
                _newDataAvailable = false; 

                currentNoiseIntensity = _latestNoiseIntensity;
                currentNoiseSpeed = _latestNoiseSpeed;
                currentCameraTransform = _latestCameraTransform;
                currentCameraMode = _latestCameraMode; 
                currentCameraKeySpeeds = new List<float>(_latestCameraKeySpeeds); 
                currentCameraMoveSpeedFactor = _latestCameraMoveSpeedFactor; 
                currentSectionNum = _latestSectionNum; 
                currentFrameNum = _latestFrameNum; 
                Debug.Log("TcpCommunicatorV2 Update: Data copied from shared variables. Releasing lock.");
            }

            // --- Unityオブジェクトへのデータ適用 ---
            if (gridMaterial != null)
            {
                gridMaterial.SetFloat("_NoiseIntensity", currentNoiseIntensity);
                gridMaterial.SetFloat("_NoiseSpeed", currentNoiseSpeed);
                Debug.Log($"Update - Applied Noise: I={currentNoiseIntensity}, S={currentNoiseSpeed}");
            }

            if (cameraObject != null && cameraView != null)
            {
                switch (currentCameraMode)
                {
                    case 0: // キーバインドモード
                        if (currentCameraKeySpeeds != null && currentCameraKeySpeeds.Count == 6)
                        {
                            cameraView.ApplyKeyMovement(
                                currentCameraKeySpeeds[0], currentCameraKeySpeeds[1], currentCameraKeySpeeds[2],
                                currentCameraKeySpeeds[3], currentCameraKeySpeeds[4], currentCameraKeySpeeds[5]
                            );
                            Debug.Log($"Camera Mode: Keybind. Applied Speeds: {string.Join(", ", currentCameraKeySpeeds)}");
                        }
                        break;
                    case 1: // Followモード
                        if (pipeServer != null && pipeServer.body != null && pipeServer.body.head != null)
                        {
                            cameraView.SetFollowTarget(pipeServer.body.head.transform); 
                            cameraView.EnableFollowMode(); 
                            Debug.Log("Camera Mode: Follow. Target set.");
                        }
                        else
                        {
                            Debug.LogWarning("Followモード: ターゲット（アバターの頭部）が見つかりません。カメラモードを無効化します。");
                            cameraView.DisableFollowMode(); 
                            cameraView.DisableAllModes();   
                        }
                        break;
                    case 2: // Vectorモード
                        cameraView.MoveToVectorPosition(currentCameraTransform, currentCameraMoveSpeedFactor); 
                        Debug.Log($"Camera Mode: Vector. Target Transform: {currentCameraTransform}, Speed Factor: {currentCameraMoveSpeedFactor}");
                        break;
                    default:
                        Debug.LogWarning($"Unknown camera mode: {currentCameraMode}. Disabling CameraView.");
                        cameraView.DisableAllModes();
                        break;
                }
            } 
            else if (cameraObject != null && cameraView == null) 
            {
                Debug.LogWarning("CameraObjectにCameraViewコンポーネントがアタッチされていません。"); 
            }

            // UIManagerにデータを渡す
            if (uiManager != null)
            {
                uiManager.UpdateSectionNumUI(currentSectionNum);
                uiManager.UpdateFrameNumUI(currentFrameNum);
                uiManager.UpdateCameraTransformUI(cameraObject.transform.position); // UIには現在のカメラ位置を渡す
                uiManager.UpdateNoiseIntensityUI(currentNoiseIntensity);
                uiManager.UpdateNoiseSpeedUI(currentNoiseSpeed);
            }

            Debug.Log("TcpCommunicatorV2 Update: Finished applying data.");
        }
        // else
        // {
        //     Debug.Log("TcpCommunicatorV2 Update: _newDataAvailable is FALSE. Skipping data application.");
        // }
    }

    // TCPリスナーを別スレッドで開始し、クライアント接続を待機
    private void ListenForClients()
    {
        Debug.Log("ListenForClients thread started.");
        try
        {
            tcpListener = new TcpListener(System.Net.IPAddress.Any, port);
            tcpListener.Start();
            Debug.Log($"TCP Listener successfully started and listening on port {port}.");

            while (true) // 常に新しいクライアント接続を待機
            {
                Debug.Log("Waiting for client connection...");
                TcpClient client = tcpListener.AcceptTcpClient();
                Debug.Log("Client Connected: " + client.Client.RemoteEndPoint);

                // 各クライアントとの通信を新しいスレッドで処理
                Thread clientThread = new Thread(() => HandleClientComm(client));
                clientThread.IsBackground = true;
                clientThread.Start();
            }
        }
        catch (SocketException socketException)
        {
            Debug.LogError("SocketException in ListenForClients: " + socketException.ToString()); 
            if (tcpListener != null) tcpListener.Stop();
        }
        catch (ThreadAbortException)
        {
            Debug.Log("ListenForClients thread aborted gracefully.");
        }
        catch (Exception ex)
        {
            Debug.LogError("Unexpected error in ListenForClients: " + ex.ToString()); 
        }
        finally
        {
            Debug.Log("ListenForClients thread finally block executed.");
            if (tcpListener != null) tcpListener.Stop(); 
        }
    }

    // 各クライアントからの通信を処理 (受信と応答送信)
    private void HandleClientComm(TcpClient client)
    {
        Debug.Log($"HandleClientComm thread started for client: {client.Client.RemoteEndPoint}");
        NetworkStream stream = client.GetStream();
        StreamReader reader = new StreamReader(stream, Encoding.UTF8); 
        StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false)); 

        try
        {
            while (client.Connected) // クライアントが接続されている間ループ
            {
                // --- 1. Pythonからのデータ受信 ---
                string clientMessage = reader.ReadLine(); 

                if (clientMessage != null)
                {
                    // 受信データをメインスレッドで処理するためのキューイング
                    // ProcessReceivedDataは受信したデータを使ってUnityオブジェクトを更新
                    UnityMainThreadDispatcher.Dispatcher.Enqueue(() => ProcessReceivedData(clientMessage)); 

                    // --- 2. Unityからの応答データ送信 ---
                    // 受信処理後、Pythonへフィードバックデータを送信
                    // この処理は受信スレッド内で行うため、Unityオブジェクトのデータ取得はメインスレッドからキューイングする
                    // ただし、ここではメインスレッドがデータを更新するのを待つ必要があるため、
                    // _newDataAvailableフラグがリセットされるのを待つか、
                    // 別の方法で最新のカメラ/ターゲット情報を取得する必要がある。
                    // 簡単化のため、ここでは最新のカメラ位置とターゲット位置を直接取得する。
                    // 注意: メインスレッドで更新されたばかりのデータが確実に反映されるとは限らない。
                    
                    Vector3 currentCamPos = Vector3.zero;
                    Vector3 currentTargetPos = Vector3.zero;
                    int currentCamMode = 0;

                    // メインスレッドのデータが更新されるのを待つ（オプション、複雑になる）
                    // または、メインスレッドで更新された_latest変数をロックして読み込む
                    lock (_latestDataLock)
                    {
                        currentCamPos = cameraObject != null ? cameraObject.transform.position : Vector3.zero;
                        currentTargetPos = (pipeServer != null && pipeServer.body != null && pipeServer.body.head != null) ? pipeServer.body.head.transform.position : Vector3.zero;
                        currentCamMode = _latestCameraMode; // 受信したばかりのモードをフィードバック
                    }

                    UnityFeedbackData feedbackData = new UnityFeedbackData(
                        _latestFrameNum, // 受信したばかりのフレーム番号
                        currentCamPos,
                        currentTargetPos,
                        currentCamMode,
                        "ACK from Unity"
                    );
                    string jsonFeedback = JsonConvert.SerializeObject(feedbackData);
                    
                    writer.WriteLine(jsonFeedback); // JSONフィードバックを送信
                    writer.Flush();
                    Debug.Log($"Sent feedback to Python: {jsonFeedback.Substring(0, Math.Min(jsonFeedback.Length, 100))}...");
                }
                else
                {
                    Debug.Log("Client disconnected gracefully: " + client.Client.RemoteEndPoint);
                    break;
                }
            }
        }
        catch (IOException ioException)
        {
            Debug.LogError("IOException in HandleClientComm (Client Disconnected): " + ioException.ToString());
        }
        catch (ThreadAbortException)
        {
            Debug.Log($"HandleClientComm thread aborted for client: {client.Client.RemoteEndPoint}");
        }
        catch (Exception e)
        {
            Debug.LogError("Error in HandleClientComm for client " + client.Client.RemoteEndPoint + ": " + e.ToString());
        }
        finally
        {
            Debug.Log($"Client resources closing for: {client.Client.RemoteEndPoint}");
            if (writer != null) writer.Close();
            if (reader != null) reader.Close();
            if (stream != null) stream.Close();
            if (client != null) client.Close();
            Debug.Log("Client resources closed: " + client.Client.RemoteEndPoint);
        }
    }

    // メインスレッドで実行される受信データ処理ロジック
    private void ProcessReceivedData(string jsonMessage) 
    {
        Debug.Log("ProcessReceivedData called. Raw JSON (first 100 chars): " + jsonMessage.Substring(0, Math.Min(jsonMessage.Length, 100)) + "...");
        receivedRawData = jsonMessage;

        try
        {
            PoseData receivedPoseData = JsonConvert.DeserializeObject<PoseData>(jsonMessage);

            if (receivedPoseData != null)
            {
                // ランドマークデータの処理 (PipeServerV2に渡す)
                if (receivedPoseData.landmarks != null && receivedPoseData.landmarks.Count == 23)
                {
                    for (int i = 0; i < receivedPoseData.landmarks.Count; i++)
                    {
                        List<float> coords = receivedPoseData.landmarks[i];
                        if (coords != null && coords.Count == 3)
                        {
                            if (pipeServer != null && pipeServer.body != null)
                            {
                                pipeServer.body.positionsBuffer[i].value = new Vector3(coords[0], coords[1], coords[2]);
                                pipeServer.body.positionsBuffer[i].accumulatedValuesCount = pipeServer.samplesForPose;
                            }
                        }
                    }
                    if (pipeServer != null && pipeServer.body != null)
                    {
                        pipeServer.body.active = true;
                    }
                }

                // 受信したShaderパラメータとカメラ位置・モード・速度・UI情報を_latest変数に格納 (lockで保護)
                lock (_latestDataLock)
                {
                    _latestNoiseIntensity = receivedPoseData.noiseIntensity;
                    _latestNoiseSpeed = receivedPoseData.noiseSpeed;
                    
                    if (receivedPoseData.cameraTransform != null && receivedPoseData.cameraTransform.Count == 3)
                    {
                        _latestCameraTransform = new Vector3(
                            receivedPoseData.cameraTransform[0],
                            receivedPoseData.cameraTransform[1],
                            receivedPoseData.cameraTransform[2]
                        );
                    }
                    
                    _latestCameraMode = receivedPoseData.cameraMode;
                    if (receivedPoseData.cameraKeySpeeds != null && receivedPoseData.cameraKeySpeeds.Count == 6)
                    {
                        _latestCameraKeySpeeds = new List<float>(receivedPoseData.cameraKeySpeeds);
                    }
                    else
                    {
                        _latestCameraKeySpeeds = new List<float>(new float[6]); 
                        Debug.LogWarning("Received cameraKeySpeeds is null or not 6 elements. Setting to default zeros.");
                    }
                    _latestCameraMoveSpeedFactor = receivedPoseData.cameraMoveSpeedFactor; 
                    _latestSectionNum = receivedPoseData.sectionNum; 
                    _latestFrameNum = receivedPoseData.frameNum; 

                    _newDataAvailable = true; // 新しいデータが利用可能になったことをフラグで通知
                    Debug.Log("ProcessReceivedData: Data updated in shared variables. _newDataAvailable set to TRUE. Releasing lock.");
                }

                receivedParam = receivedPoseData.param;

                Debug.Log($"受信スレッド - 解析結果: FrameNum={receivedPoseData.frameNum}, NoiseIntensity={receivedPoseData.noiseIntensity}, NoiseSpeed={receivedPoseData.noiseSpeed}, CameraMode={receivedPoseData.cameraMode}, KeySpeeds={string.Join(", ", receivedPoseData.cameraKeySpeeds)}, CameraTransform={receivedPoseData.cameraTransform[0]},{receivedPoseData.cameraTransform[1]},{receivedPoseData.cameraTransform[2]}, MoveSpeedFactor={receivedPoseData.cameraMoveSpeedFactor}, SectionNum={receivedPoseData.sectionNum}");
            }
            else
            {
                Debug.LogError("Deserialized PoseData is null. Raw: " + jsonMessage);
            }
        }
        catch (JsonSerializationException jsonEx)
        {
            Debug.LogError("JSON Serialization Error: " + jsonEx.ToString() + " Raw: " + jsonMessage);
        }
        catch (Exception ex)
        {
            Debug.LogError("Error processing received data: " + ex.ToString() + " Raw: " + jsonMessage);
        }
    }
}