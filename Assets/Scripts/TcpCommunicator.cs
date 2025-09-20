using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using Newtonsoft.Json;
using UnityMainThreadDispatcher; // UnityMainThreadDispatcher が必要です

public class TcpCommunicator : MonoBehaviour
{
    public int port = 8080;
    public PipeServer pipeServer; // PipeServerへの参照
    public GameObject gridObject; // Shader変数を持つマテリアルが適用されたオブジェクト
    public GameObject cameraObject; // カメラの位置を更新するオブジェクト（CameraViewがアタッチされているもの）
    public UIManager uiManager; // UIManagerへの参照
    
    // 他のスクリプトへの参照
    private CameraView cameraView; 

    // マテリアルインスタンスを保持する変数
    private Material gridMaterial; 

    // 受信した生のデータを格納する変数（デバッグ用）
    public string receivedRawData;
    public float receivedParam;
    public Vector3[] receivedLandmarks = new Vector3[23];

    // 最新のShaderパラメータ、カメラ位置、モード、速度、UI情報を保持する変数
    // これらは受信スレッドから書き込まれ、メインスレッドから読み込まれます
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

    //特長量の格納
    public float[] features; // 特徴量を格納する配列



    void Start()
    {
        Application.targetFrameRate = 30;
        Debug.Log("TcpCommunicatorV2 Start called.");

        // Newtonsoft.Jsonの存在確認
        if (typeof(JsonConvert) == null)
        {
            Debug.LogError("Newtonsoft.Json (Json.NET) not found. Please import it into your Unity project.");
            return;
        }

        // PipeServerV2の参照を取得
        if (pipeServer == null)
        {
            pipeServer = FindObjectOfType<PipeServer>(); // シーン内のPipeServerV2を探す
            if (pipeServer == null)
            {
                Debug.LogError("PipeServerV2 script not found in the scene! Please add it to a GameObject and assign it in the Inspector.");
                return;
            }
        }

        // GridObjectからマテリアルを取得し、初期値を設定
        if (gridObject != null)
        {
            Renderer gridRenderer = gridObject.GetComponent<Renderer>();
            if (gridRenderer != null)
            {
                gridMaterial = gridRenderer.material; // Rendererからマテリアルを取得
                _latestNoiseIntensity = gridMaterial.GetFloat("_NoiseIntensity");
                _latestNoiseSpeed = gridMaterial.GetFloat("_NoiseSpeed");
                Debug.Log($"TcpServerV2: Initial NoiseIntensity: {_latestNoiseIntensity}, NoiseSpeed: {_latestNoiseSpeed}");
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

        // CameraViewコンポーネントの参照を取得
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
            _latestCameraTransform = Vector3.zero; // cameraObjectが設定されていなければVector3.zero
            Debug.LogWarning("CameraObjectが設定されていません。カメラの初期位置はVector3.zeroです。");
        }

        // UIManagerの参照を自動取得 (Inspectorで設定しない場合)
        if (uiManager == null)
        {
            uiManager = FindObjectOfType<UIManager>();
            if (uiManager == null)
            {
                Debug.LogWarning("UIManager script not found in the scene! Please add it to a GameObject and assign it in the Inspector.");
            }
        }

        // 最新データ保持変数の初期値設定
        _latestCameraMode = 0;
        _latestCameraKeySpeeds = new List<float>(new float[6]);
        _latestCameraMoveSpeedFactor = 0f; 
        _latestSectionNum = 0; 
        _latestFrameNum = 0; 

        // TCPリスナーを別のスレッドで開始
        listenThread = new Thread(new ThreadStart(ListenForClients));
        listenThread.IsBackground = true; // アプリケーション終了時に自動終了
        listenThread.Start();
        Debug.Log("TCP Server Started on port " + port);
    }

    // アプリケーション終了時のクリーンアップ
    void OnApplicationQuit()
    {
        Debug.Log("TcpServerV2 OnApplicationQuit called. Attempting to stop TCP Listener and Thread.");
        if (tcpListener != null)
        {
            tcpListener.Stop();
            Debug.Log("TCP Listener Stopped.");
        }
        if (listenThread != null && listenThread.IsAlive)
        {
            listenThread.Abort(); // スレッドを強制終了
            Debug.Log("Listen Thread Aborted.");
        }
    }

    // Updateメソッドはメインスレッドで実行され、受信データに基づいてUnityオブジェクトを更新
    void Update()
    {
        // Debug.Log("TcpServerV2 Update called. _newDataAvailable: " + _newDataAvailable); // 頻繁なので必要に応じてコメントアウト

        // 新しいデータが受信されていれば（不必要なロックを避けるための事前チェック）
        if (_newDataAvailable) 
        {
            Debug.Log("TcpServerV2 Update: _newDataAvailable is TRUE. Attempting to acquire lock.");

            // 共有変数から安全にデータを読み込むためのローカル変数
            float currentNoiseIntensity;
            float currentNoiseSpeed;
            Vector3 currentCameraTransform;
            int currentCameraMode;
            List<float> currentCameraKeySpeeds;
            float currentCameraMoveSpeedFactor;
            int currentSectionNum;
            int currentFrameNum;

            // ロックして最新の値を安全に読み込む
            lock (_latestDataLock)
            {
                Debug.Log("TcpServerV2 Update: Lock acquired. Resetting _newDataAvailable.");
                _newDataAvailable = false; // データを使用したのでフラグをリセット

                // 共有変数からローカル変数に値をコピー
                currentNoiseIntensity = _latestNoiseIntensity;
                currentNoiseSpeed = _latestNoiseSpeed;
                currentCameraTransform = _latestCameraTransform;
                currentCameraMode = _latestCameraMode; 
                currentCameraKeySpeeds = new List<float>(_latestCameraKeySpeeds); 
                currentCameraMoveSpeedFactor = _latestCameraMoveSpeedFactor; 
                currentSectionNum = _latestSectionNum; 
                currentFrameNum = _latestFrameNum; 
                Debug.Log("TcpServerV2 Update: Data copied from shared variables. Releasing lock.");
            }

            // ノイズパラメータの適用
            if (gridMaterial != null)
            {
                gridMaterial.SetFloat("_NoiseIntensity", currentNoiseIntensity);
                gridMaterial.SetFloat("_NoiseSpeed", currentNoiseSpeed);
                //Debug.Log($"Update - Applied Noise: I={currentNoiseIntensity}, S={currentNoiseSpeed}");
            }

            // カメラ制御の適用
            if (cameraObject != null && cameraView != null)
            {
                // CameraViewスクリプトの通常のキー入力処理はCameraView内のisExternalControlで制御されるため、ここではスクリプト全体を無効化しない

                switch (currentCameraMode)
                {
                    case 0: // キーバインドモード
                        if (currentCameraKeySpeeds != null && currentCameraKeySpeeds.Count == 6)
                        {
                            cameraView.ApplyKeyMovement(
                                currentCameraKeySpeeds[0], currentCameraKeySpeeds[1], currentCameraKeySpeeds[2],
                                currentCameraKeySpeeds[3], currentCameraKeySpeeds[4], currentCameraKeySpeeds[5]
                            );
                            //Debug.Log($"Camera Mode: Keybind. Applied Speeds: {string.Join(", ", currentCameraKeySpeeds)}");
                        }
                        break;
                    case 1: // Followモード
                        if (pipeServer != null && pipeServer.body != null && pipeServer.body.head != null)
                        {
                            cameraView.SetFollowTarget(pipeServer.body.head.transform); 
                            cameraView.EnableFollowMode(); 
                            //Debug.Log("Camera Mode: Follow. Target set.");
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
                uiManager.UpdateCameraTransformUI(currentCameraTransform);
                uiManager.UpdateNoiseIntensityUI(currentNoiseIntensity);
                uiManager.UpdateNoiseSpeedUI(currentNoiseSpeed);
            }

            //Debug.Log("TcpServerV2 Update: Finished applying data.");
        }
        else
        {
            // Debug.Log("TcpServerV2 Update: _newDataAvailable is FALSE. Skipping data application.");
        }
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
                TcpClient client = tcpListener.AcceptTcpClient(); // クライアント接続をブロックして待機
                Debug.Log("Client Connected: " + client.Client.RemoteEndPoint);

                // 各クライアントとの通信を新しいスレッドで処理
                Thread clientThread = new Thread(() => HandleClientComm(client));
                clientThread.IsBackground = true;
                clientThread.Start();
            }
        }
        catch (SocketException socketException)
        {
            // SocketExceptionが発生した場合、スタックトレースを含めてログ出力
            Debug.LogError("SocketException in ListenForClients: " + socketException.ToString()); 
            if (tcpListener != null) tcpListener.Stop();
        }
        catch (ThreadAbortException)
        {
            // スレッドが中断された場合の正常終了ログ
            Debug.Log("ListenForClients thread aborted gracefully.");
        }
        catch (Exception ex)
        {
            // その他の予期せぬエラーの場合、詳細なログ出力
            Debug.LogError("Unexpected error in ListenForClients: " + ex.ToString()); 
        }
        finally
        {
            Debug.Log("ListenForClients thread finally block executed.");
            if (tcpListener != null) tcpListener.Stop(); // リスナーが確実に停止されるように
        }
    }

    // 各クライアントからの通信を処理
    private void HandleClientComm(TcpClient client)
    {
        Debug.Log($"HandleClientComm thread started for client: {client.Client.RemoteEndPoint}");
        NetworkStream stream = client.GetStream();
        StreamReader reader = new StreamReader(stream, Encoding.UTF8); // UTF-8で読み込み
        StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false)); // BOMなしUTF-8で書き込み

        try
        {
            while (client.Connected) // クライアントが接続されている間ループ
            {
                string clientMessage = reader.ReadLine(); // 改行コードまで読み込む

                if (clientMessage != null)
                {

                        // 送信データをJSON形式で準備
                    // メインスレッドで送信データを準備して送信
                    UnityMainThreadDispatcher.Dispatcher.Enqueue(() => {
                        var sendData = new 
                        {
                            head_position = pipeServer?.body?.head?.transform.position ?? Vector3.zero,
                            camera_position = cameraObject?.transform.position ?? Vector3.zero
                        };
                        
                        string jsonSendMessage = JsonConvert.SerializeObject(sendData);
                        Debug.Log("Sending data to client: " + jsonSendMessage);
                        
                        // メインスレッドから送信
                        writer.WriteLine(jsonSendMessage);
                        writer.Flush();
                    });

                    // メインスレッドで受信データを処理し、Unityオブジェクトに適用するためのキューイング
                    // ProcessReceivedDataにwriterを渡す必要がなくなる
                    UnityMainThreadDispatcher.Dispatcher.Enqueue(() => ProcessReceivedData(clientMessage)); 

                }
                else
                {
                    // クライアントが切断した場合 (ReadLineがnullを返す)
                    Debug.Log("Client disconnected gracefully: " + client.Client.RemoteEndPoint);
                    break;
                }
            }
        }
        catch (IOException ioException)
        {
            // IOエラー (クライアント切断など) の場合、詳細なログ出力
            Debug.LogError("IOException in HandleClientComm (Client Disconnected): " + ioException.ToString());
        }
        catch (ThreadAbortException)
        {
            // スレッドが中断された場合のログ
            Debug.Log($"HandleClientComm thread aborted for client: {client.Client.RemoteEndPoint}");
        }
        catch (Exception e)
        {
            // その他の予期せぬエラーの場合、詳細なログ出力
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
        Debug.Log("ProcessReceivedData called. Raw JSON (first 100 chars): " + jsonMessage.Substring(0, Math.Min(jsonMessage.Length, 2000)) + "...");
        receivedRawData = jsonMessage;

        try
        {
            // PoseDataクラスに直接デシリアライズ
            PoseData receivedPoseData = JsonConvert.DeserializeObject<PoseData>(jsonMessage);
            
            Debug.Log("deserialized features: " + (receivedPoseData.features != null ? string.Join(", ", receivedPoseData.features) : "null"));
            // 特徴量の処理
            if (receivedPoseData.features != null && receivedPoseData.features.Count > 0)
                {
                    features = new float[receivedPoseData.features.Count];
                    for (int i = 0; i < receivedPoseData.features.Count; i++)
                    {
                        features[i] = receivedPoseData.features[i];
                    }
                }
            else
                {
                    features = new float[0]; // 特徴量がない場合は空の配列
                    Debug.LogWarning("Received features is null or empty. Setting to empty array.");
                }

                Debug.Log("features in tcpCommunicator: " + (features != null ? string.Join(", ", features) : "null"));

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

                    // cameraTransformはXYZ座標のみを受け取る
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


                //Debug.Log($"受信スレッド - 解析結果: section={_latestSectionNum },  NoiseIntensity={receivedPoseData.noiseIntensity}, NoiseSpeed={receivedPoseData.noiseSpeed}, CameraMode={receivedPoseData.cameraMode}, KeySpeeds={string.Join(", ", receivedPoseData.cameraKeySpeeds)}, CameraTransform={receivedPoseData.cameraTransform[0]},{receivedPoseData.cameraTransform[1]},{receivedPoseData.cameraTransform[2]}, MoveSpeedFactor={receivedPoseData.cameraMoveSpeedFactor}, SectionNum={receivedPoseData.sectionNum}");
            }
            else
            {
                Debug.LogError("Deserialized PoseData is null. Raw: " + jsonMessage);
            }
        }
        catch (JsonSerializationException jsonEx)
        {
            Debug.LogError("JSON Serialization Error (check PoseData class definition or JSON format): " + jsonEx.ToString() + " Raw: " + jsonMessage);
        }
        catch (Exception ex)
        {
            Debug.LogError("Error processing received data: " + ex.ToString() + " Raw: " + jsonMessage);
        }
    }
}