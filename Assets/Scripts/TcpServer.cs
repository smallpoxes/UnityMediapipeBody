using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;
using System.Collections.Concurrent;
using System.Collections.Generic; // Listを使用するために追加

public class TcpServer : MonoBehaviour
{
    public bool useSlider;
    // cameraPositionはInspectorから設定されるため、フィールドとしては不要
    // private Vector3 cameraPosition; 
    [Range(0f, 0.00001f)] 
    public float noiseSpeed; // 初期値としてInspectorから設定される
    [Range(0f, 1.5f)] 
    public float noiseIntensity; // 初期値としてInspectorから設定される
    private Material material; // gridObjectのRendererから取得するマテリアル
    public int port = 5050; // 待ち受けるポート番号
    private TcpListener tcpListener; // TCPサーバーのリスナー
    private CancellationTokenSource cancellationTokenSource; // 非同期処理キャンセルトークン

    // Unity Editorから設定するGameObjectへの参照
    public GameObject gridObject; 
    public GameObject targetObject; 
    public GameObject cameraObject; 

    // --- 共有データ: 受信した最新の値を保持する変数 ---
    // volatileキーワードはVector3のような複合型には使えないため削除。
    // 代わりに_latestDataLockでアクセスを保護します。
    private float _latestNoiseIntensity;
    private float _latestNoiseSpeed;
    private Vector3 _latestCameraTransform; 
    private bool _newDataAvailable = false; // 新しいデータが利用可能かを示すフラグ

    // 複数のスレッド間（受信スレッドとメインスレッド）で上記データを安全に保護するためのロックオブジェクト
    private readonly object _latestDataLock = new object();

    // メインスレッドで実行する必要がある処理のためのキュー (デバッグログなど、Unity APIを呼ぶための用途)
    private ConcurrentQueue<Action> mainThreadActions = new ConcurrentQueue<Action>();

    // 接続中のクライアントを管理するリスト (ロックで保護)
    private readonly List<TcpClient> connectedClients = new List<TcpClient>();
    private readonly object clientsLock = new object(); // connectedClients を保護するためのロックオブジェクト

    // --- 新規追加: 解析されたデータの結果を保持する構造体 ---
    /// <summary>
    /// TCPで受信したメッセージから解析されたデータを含む構造体。
    /// 各フィールドはNullable型であり、メッセージにその値が含まれていなかった場合はnullになる。
    /// </summary>
    public struct ParsedTcpData
    {
        public float? NoiseIntensity; // ノイズ強度が解析された場合の値
        public float? NoiseSpeed;     // ノイズ速度が解析された場合の値
        public Vector3? CameraTransform; // カメラ変換が解析された場合の値
    }
    // --- 新規追加ここまで ---

    /// <summary>
    /// スクリプト開始時に呼び出されます。TCPサーバーを起動し、マテリアルの初期値を設定します。
    /// </summary>
    void Start()
    {
        // gridObjectとRendererコンポーネントの存在を確認
        if (gridObject != null)
        {
            Renderer gridRenderer = gridObject.GetComponent<Renderer>();
            if (gridRenderer != null)
            {
                material = gridRenderer.material; // Rendererからマテリアルを取得
                
                // Inspectorで設定された初期値をマテリアルに適用
                material.SetFloat("_NoiseIntensity", noiseIntensity);
                material.SetFloat("_NoiseSpeed", noiseSpeed);

                // _latest変数にも初期値を設定。これにより、最初の受信データに特定のキーがない場合でも、
                // 初期値が保持され、0になることを防ぎます。
                _latestNoiseIntensity = noiseIntensity;
                _latestNoiseSpeed = noiseSpeed;
            }
            else
            {
                Debug.LogError("GridObjectにRendererコンポーネントが見つかりません。マテリアル設定不可。");
            }
        }
        else
        {
            Debug.LogError("GridObjectが設定されていません。");
        }

        // cameraObjectの初期位置を_latestCameraTransformに設定
        if (cameraObject != null)
        {
            _latestCameraTransform = cameraObject.transform.position;
        }
        else
        {
            _latestCameraTransform = Vector3.zero; // cameraObjectが設定されていなければVector3.zero
            Debug.LogWarning("CameraObjectが設定されていません。カメラの初期位置はVector3.zeroです。");
        }

        cancellationTokenSource = new CancellationTokenSource(); // キャンセルトークンを初期化
        StartTcpServer(); // TCPサーバーを開始
    }

    /// <summary>
    /// 受信した文字列データからノイズ強度、ノイズ速度、カメラ変換を抽出します。
    /// このメソッドは純粋に文字列解析のみを行い、Unityオブジェクトの現在の値には依存しません。
    /// </summary>
    /// <param name="inputString">受信した入力文字列（例: "_NoiseIntensity:0.5,_NoiseSpeed:0.00001,cameratransform:10/5.9/-30.7,"）</param>
    /// <returns>解析されたデータを含むParsedTcpData構造体。値が解析できなかった場合は対応するフィールドはnull。</returns>
    public static ParsedTcpData ExtractData(string inputString) // 引数からMaterialとGameObjectを削除
    {
        ParsedTcpData parsedData = new ParsedTcpData(); // Nullableフィールドはデフォルトでnull

        // 文字列をカンマで分割
        string[] parts = inputString.Split(',');

        // デバッグログ: ExtractDataに渡された生データ（コメントアウト解除して確認）
        // Debug.Log($"ExtractData - InputString: '{inputString}'");
        // Debug.Log($"ExtractData - Parts[0]: '{parts.Length > 0 ? parts[0] : "N/A"}'"); // 最初はここが不完全だった

        foreach (string part in parts)
        {
            // 各パートをキーと値に分割
            string[] keyValue = part.Split(':');
            if (keyValue.Length != 2)
            {
                // フォーマットが不正な場合はスキップ
                continue;
            }
            
            string key = keyValue[0].Trim();
            string value = keyValue[1].Trim();

            // CultureInfo.InvariantCulture を使用して、カルチャに依存しない数値解析を行う
            // これにより、小数点記号が常に '.' として扱われます
            if (key.Equals("_NoiseIntensity", StringComparison.OrdinalIgnoreCase))
            {
                if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float intensity))
                {
                    parsedData.NoiseIntensity = intensity; // 解析成功した場合のみ設定
                    // Debug.Log($"ExtractData - Parsed _NoiseIntensity: {intensity}");
                }
                // else { Debug.LogWarning($"ExtractData - Failed to parse _NoiseIntensity value: '{value}'"); }
            }
            else if (key.Equals("_NoiseSpeed", StringComparison.OrdinalIgnoreCase))
            {
                if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float speed))
                {
                    parsedData.NoiseSpeed = speed; // 解析成功した場合のみ設定
                    // Debug.Log($"ExtractData - Parsed _NoiseSpeed: {speed}");
                }
                // else { Debug.LogWarning($"ExtractData - Failed to parse _NoiseSpeed value: '{value}'"); }
            }
            else if (key.Equals("cameratransform", StringComparison.OrdinalIgnoreCase))
            {
                string[] coords = value.Split('/');
                if (coords.Length == 3)
                {
                    float x, y, z;
                    if (float.TryParse(coords[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x) &&
                        float.TryParse(coords[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y) &&
                        float.TryParse(coords[2], NumberStyles.Float, CultureInfo.InvariantCulture, out z))
                    {
                        parsedData.CameraTransform = new Vector3(x, y, z); // 解析成功した場合のみ設定
                        // Debug.Log($"ExtractData - Parsed CameraTransform: {parsedData.CameraTransform.Value}");
                    }
                    // else { Debug.LogWarning($"ExtractData - Failed to parse CameraTransform coordinates: '{value}'"); }
                }
                // else { Debug.LogWarning($"ExtractData - Invalid CameraTransform format: '{value}'"); }
            }
        }
        // ExtractDataの最終的なログ出力は不要。呼び出し元でParsedTcpDataを確認する。
        // Debug.Log($"ExtractData - Parsed Result: NoiseIntensity={parsedData.NoiseIntensity}, NoiseSpeed={parsedData.NoiseSpeed}, CameraTransform={parsedData.CameraTransform}");
        return parsedData;
    }
            
    /// <summary>
    /// アプリケーション終了時に呼び出されます。TCPサーバーを停止します。
    /// </summary>
    void OnApplicationQuit()
    {
        StopTcpServer();
    }

    /// <summary>
    /// フレームごとに呼び出されます。メインスレッドで実行されるアクションを処理し、最新の受信データを適用します。
    /// </summary>
    void Update()
    {
        // メインスレッドで実行する必要がある他のアクションを処理 (例: デバッグログの出力)
        while (mainThreadActions.TryDequeue(out var action))
        {
            action.Invoke();
        }

        // --- メインスレッド処理: lockを使用して最新データを読み込み、Unityオブジェクトに適用 ---
        if (_newDataAvailable) // 新しいデータが受信されていれば（不必要なロックを避けるための事前チェック）
        {
            float currentNoiseIntensity;
            float currentNoiseSpeed;
            Vector3 currentCameraTransform;

            lock (_latestDataLock) // ロックして最新の値を安全に読み込む
            {
                _newDataAvailable = false; // データを使用したのでフラグをリセット

                // 共有変数からローカル変数に値をコピー
                currentNoiseIntensity = _latestNoiseIntensity;
                currentNoiseSpeed = _latestNoiseSpeed;
                currentCameraTransform = _latestCameraTransform;
            }

            // メインスレッドでUnityのAPIを呼び出す
            if (material != null)
            {
                material.SetFloat("_NoiseIntensity", currentNoiseIntensity);
                material.SetFloat("_NoiseSpeed", currentNoiseSpeed);
                // Debug.Log($"Update - Applied NoiseIntensity: {currentNoiseIntensity}, NoiseSpeed: {currentNoiseSpeed}");
            }
            if (cameraObject != null)
            {
                cameraObject.transform.position = currentCameraTransform;
                // Debug.Log($"Update - Applied CameraTransform: {currentCameraTransform}");
            }
            
            // ログ: 実際に適用されたデータを表示。これが0になる瞬間があるかを確認。
            if (material != null && cameraObject != null)
            {
                Debug.Log($"実際のデータ (Update適用後): noiseintensity={material.GetFloat("_NoiseIntensity")}, noisespeed={material.GetFloat("_NoiseSpeed")}, camerapos={cameraObject.transform.position}"); 
            }
            
            // ログ: _NoiseIntensityが0.5ではない場合の異常検知
            if (material != null && Mathf.Abs(material.GetFloat("_NoiseIntensity") - 0.5f) > 0.0001f) // 浮動小数点比較は誤差を考慮
            {
                Debug.LogWarning($"!!! 異常を検知 (Update適用後): noiseintensity={material.GetFloat("_NoiseIntensity")}, noisespeed={material.GetFloat("_NoiseSpeed")}, camerapos={cameraObject.transform.position} !!!"); 
            }
        }
        // --- メインスレッド処理ここまで ---

        // targetObjectとcameraObjectが設定されていることを確認してから送信
        if (targetObject != null && cameraObject != null)
        {
            SendParameterToAllClients(targetObject.transform.position - cameraObject.transform.position);
        }
    }

    /// <summary>
    /// TCPサーバーを開始します。非同期でクライアント接続を待ち受け、各クライアントの処理を開始します。
    /// </summary>
    private async void StartTcpServer()
    {
        try
        {
            tcpListener = new TcpListener(IPAddress.Any, port);
            tcpListener.Start();
            Debug.Log($"TCPサーバーがポート {port} で開始されました。");

            while (!cancellationTokenSource.IsCancellationRequested)
            {
                TcpClient client = await tcpListener.AcceptTcpClientAsync();
                Debug.Log($"新しいクライアントが接続しました: {((IPEndPoint)client.Client.RemoteEndPoint).Address}");

                // クライアントリストに追加 (ロックで保護)
                lock (clientsLock)
                {
                    connectedClients.Add(client);
                }

                // クライアント処理を非同期タスクとして開始し、タスクの完了を待たない (_ を使用)
                _ = HandleClientAsync(client, cancellationTokenSource.Token);
            }
        }
        catch (SocketException socketException)
        {
            Debug.LogError($"ソケットエラーが発生しました: {socketException.Message}");
        }
        catch (Exception e)
        {
            Debug.LogError($"TCPサーバーの開始中に予期せぬエラーが発生しました: {e.Message}");
        }
    }

    /// <summary>
    /// 個々のクライアント接続を処理します。受信データをバッファリングし、完全なメッセージを解析します。
    /// </summary>
    /// <param name="client">処理するTcpClientオブジェクト</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        NetworkStream stream = null;
        StringBuilder messageBuffer = new StringBuilder(); // 受信データを一時的に保持するバッファ
        try
        {
            stream = client.GetStream();
            byte[] buffer = new byte[1024]; // ストリームから読み込むための一時バッファ

            while (client.Connected && !cancellationToken.IsCancellationRequested)
            {
                // データを受信
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);

                if (bytesRead == 0) // クライアントが切断された場合
                {
                    Debug.Log($"クライアントが切断されました: {((IPEndPoint)client.Client.RemoteEndPoint).Address}");
                    break;
                }

                string receivedChunk = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                messageBuffer.Append(receivedChunk); // 受信したチャンクをバッファに追加

                // メッセージデリミタ（例: '\n'）で区切るループ
                // バッファから完全なメッセージを抽出し、処理します
                while (true) // メッセージバッファ内に改行が複数含まれる可能性があるのでループ
                {
                    string currentBufferContent = messageBuffer.ToString();
                    int newlineIndex = currentBufferContent.IndexOf('\n');

                    if (newlineIndex == -1)
                    {
                        // 改行コードが見つからない場合、完全なメッセージはまだ受信されていないので、ループを抜けて次のデータを受信する
                        break; 
                    }

                    // 完全なメッセージを抽出 (改行コードは含まない)
                    string completeMessage = currentBufferContent.Substring(0, newlineIndex).Trim();
                    
                    // 処理したメッセージの長さ + 改行コードの分をバッファから削除
                    messageBuffer.Remove(0, newlineIndex + 1);

                    if (string.IsNullOrWhiteSpace(completeMessage))
                    {
                        // 空のメッセージ（例: 連続した改行）はスキップ
                        continue;
                    }

                    // ここで完全なメッセージを処理
                    ParsedTcpData parsedData = ExtractData(completeMessage); // 修正版のExtractDataを呼び出す

                    // --- 受信スレッド処理: lockを使用して最新データを_latest変数に書き込む ---
                    lock (_latestDataLock) // ロックして最新の値を安全に書き込む
                    {
                        // 解析された値が存在する場合のみ、_latest変数を更新
                        if (parsedData.NoiseIntensity.HasValue)
                        {
                            _latestNoiseIntensity = parsedData.NoiseIntensity.Value;
                        }
                        if (parsedData.NoiseSpeed.HasValue)
                        {
                            _latestNoiseSpeed = parsedData.NoiseSpeed.Value;
                        }
                        if (parsedData.CameraTransform.HasValue)
                        {
                            _latestCameraTransform = parsedData.CameraTransform.Value;
                        }
                        
                        // いずれかの値が実際に更新された場合のみ、新しいデータがあるフラグを立てる
                        if (parsedData.NoiseIntensity.HasValue || parsedData.NoiseSpeed.HasValue || parsedData.CameraTransform.HasValue)
                        {
                            _newDataAvailable = true; 
                        }
                    }
                    // --- 受信スレッド処理ここまで ---

                    // デバッグログは、メインスレッドで安全に出力するためにキューに入れる
                    mainThreadActions.Enqueue(() =>
                    {
                        Debug.Log($"受信スレッド - 完全メッセージ: '{completeMessage}'");
                        Debug.Log($"受信スレッド - 解析結果: NoiseIntensity={parsedData.NoiseIntensity}, NoiseSpeed={parsedData.NoiseSpeed}, CameraTransform={parsedData.CameraTransform}");
                    });
                }
            }
        }
        catch (OperationCanceledException)
        {
            Debug.Log($"クライアント処理がキャンセルされました: {((IPEndPoint)client.Client.RemoteEndPoint).Address}");
        }
        catch (SocketException socketException)
        {
            Debug.LogError($"クライアントのソケットエラー: {socketException.Message} ({((IPEndPoint)client.Client.RemoteEndPoint).Address})");
        }
        catch (Exception e)
        {
            Debug.LogError($"クライアント処理中に予期せぬエラーが発生しました: {e.Message} ({((IPEndPoint)client.Client.RemoteEndPoint).Address})");
        }
        finally
        {
            // クライアント切断時にリストから削除 (ロックで保護)
            lock (clientsLock)
            {
                connectedClients.Remove(client);
            }
            stream?.Close();
            client?.Close();
            Debug.Log($"クライアント接続を閉じました: {((IPEndPoint)client.Client.RemoteEndPoint).Address}");
        }
    }

    /// <summary>
    /// 全ての接続中のクライアントにパラメータを送信します。
    /// </summary>
    /// <param name="vec">送信するVector3データ</param>
    public async void SendParameterToAllClients(Vector3 vec)
    {
        // Vector3を文字列に変換（例: "1.23,4.56,7.89"）
        string vecString = $"{vec.x},{vec.y},{vec.z}"; 
        byte[] dataToSend = Encoding.UTF8.GetBytes(vecString);
        List<TcpClient> clientsToSend;

        lock (clientsLock)
        {
            // スナップショットを作成して、ループ中にリストが変更されても安全にする
            clientsToSend = new List<TcpClient>(connectedClients); 
        }

        foreach (var client in clientsToSend)
        {
            if (client.Connected)
            {
                try
                {
                    NetworkStream stream = client.GetStream();
                    await stream.WriteAsync(dataToSend, 0, dataToSend.Length, cancellationTokenSource.Token);
                    // ログ: UnityからPythonへの送信。高頻度でログが出力される可能性があるので、本番環境ではコメントアウト推奨。
                    // Debug.Log($"UnityからPythonへ送信: '{vecString}' to {((IPEndPoint)client.Client.RemoteEndPoint).Address}"); 
                }
                catch (ObjectDisposedException)
                {
                    // クライアントが既に閉じられている場合。通常はHandleClientAsyncのfinallyブロックで処理される。
                    // Debug.LogWarning($"送信中にクライアント {((IPEndPoint)client.Client.RemoteEndPoint).Address} が既に閉じられていました。");
                }
                catch (Exception e)
                {
                    // その他の送信エラー
                    Debug.LogError($"UnityからPythonへの送信中にエラーが発生しました: {e.Message} to {((IPEndPoint)client.Client.RemoteEndPoint).Address}");
                }
            }
        }
    }

    /// <summary>
    /// TCPサーバーを停止し、すべてのクライアント接続を閉じます。
    /// </summary>
    private void StopTcpServer()
    {
        cancellationTokenSource?.Cancel(); // 非同期タスクにキャンセルを通知
        if (tcpListener != null)
        {
            tcpListener.Stop(); // リスナーを停止
            Debug.Log("TCPサーバーが停止されました。");
        }
        // 全てのクライアント接続を安全に閉じる (ロックで保護)
        lock (clientsLock)
        {
            foreach (var client in connectedClients)
            {
                client.Close(); // 個々のクライアント接続を閉じる
            }
            connectedClients.Clear(); // リストをクリア
        }
    }
}
