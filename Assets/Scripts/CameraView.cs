// CameraView.cs
using UnityEngine;

public class CameraView : MonoBehaviour
{
    [Header("ターゲット設定")]
    public GameObject targetObject;

    [Header("カメラ設定")]
    [Tooltip("ターゲットの周りを公転する速度（度/秒）")]
    public float rotationSpeed = 50f;
    [Tooltip("ターゲットに接近/後退する速度（ユニット/秒）")]
    public float zoomSpeed = 5f;
    [Tooltip("ターゲットを見る際の追従速度")]
    public float lookAtLerpSpeed = 5f;

    [Header("距離の制限")]
    [Tooltip("これ以上ターゲットに近づけない最小距離")]
    public float minZoomDistance = 2f;

    [Header("縦回転の角度制限")]
    [Tooltip("上方向の最大角度")]
    public float maxVerticalAngle = 80.0f;
    [Tooltip("下方向の最小角度")]
    public float minVerticalAngle = -10.0f;

    [Header("Followモード設定")]
    [Tooltip("ターゲットが画面端からこの距離内に来たら追従を開始する (0.0 - 0.5)")]
    public float followMargin = 0.2f;
    [Tooltip("追従を停止するまでの画面中央からの距離 (0.0 - 0.5)")]
    public float stopFollowMargin = 0.1f;
    [Tooltip("追従時にターゲットから維持したい理想的な距離")]
    public float desiredFollowDistance = 10f;
    [Tooltip("追従時にターゲットから維持したい理想的な高さオフセット")]
    public float desiredHeightOffset = 3f;

    [Header("Vectorモード設定")] // 新しいヘッダーを追加
    [Tooltip("Vectorモードで補間移動に使用する基本速度。大きいほど瞬間移動に近づく。")]
    public float vectorMoveBaseSpeed = 50.0f; // 調整してください。

    private Transform currentTargetTransform;
    private bool isFollowing = false;
    private bool isExternalControl = false; // 外部（TCP）から制御されているか

    // Vectorモードの目標位置と移動中フラグ
    private Vector3 vectorTargetPosition;
    private float currentVectorMoveSpeedFactor; // Pythonから受け取った速度係数 (0-1)
    private bool isMovingToVectorPosition = false; // Vectorモードで現在移動中かどうか

    void Start()
    {
        if (targetObject != null)
        {
            currentTargetTransform = targetObject.transform;
        }
        else
        {
            Debug.LogWarning("CameraView: ターゲットオブジェクトが設定されていません。手動でターゲットを設定するか、TCPServerV2からSetFollowTargetを呼び出してください。", this);
        }
        
        isExternalControl = false;
        isFollowing = false;
        // Vectorモードの初期化
        vectorTargetPosition = transform.position; // 開始位置を現在のカメラ位置に設定
        currentVectorMoveSpeedFactor = 1.0f; // デフォルトは瞬間移動に近い
        isMovingToVectorPosition = false;
    }

    void LateUpdate()
    {
         //Debug.Log($"CameraView LateUpdate: isExternalControl={isExternalControl}, isFollowing={isFollowing}, isMovingToVectorPosition={isMovingToVectorPosition}"); 
        if (isExternalControl)
        {
            if (isFollowing)
            {
                ApplyFollowMovement();
            }
            else if (isMovingToVectorPosition) // Vectorモードで補間移動中
            {
                ApplyVectorPositionMovement(); // 新しいメソッドを呼び出す
            }
            return;
        }

        // --- 以下、元のキー入力処理 (isExternalControl が false の場合のみ実行) ---
        if (currentTargetTransform == null) return;

        Vector3 directionToTarget = (currentTargetTransform.position - this.transform.position).normalized;
        float currentDistance = Vector3.Distance(this.transform.position, currentTargetTransform.position);

        // 1. 水平回転（A/Dキー）
        if (Input.GetKey(KeyCode.A))
        {
            transform.RotateAround(currentTargetTransform.position, Vector3.up, -rotationSpeed * Time.deltaTime);
        }
        if (Input.GetKey(KeyCode.D))
        {
            transform.RotateAround(currentTargetTransform.position, Vector3.up, rotationSpeed * Time.deltaTime);
        }

        // 2. 縦回転（Q/Eキー）
        if (Input.GetKey(KeyCode.Q))
        {
            transform.RotateAround(currentTargetTransform.position, this.transform.right, -rotationSpeed * Time.deltaTime);
        }
        if (Input.GetKey(KeyCode.E))
        {
            transform.RotateAround(currentTargetTransform.position, this.transform.right, rotationSpeed * Time.deltaTime);
        }
        
        // 3. ズーム（接近/後退）
        if (Input.GetKey(KeyCode.W) && currentDistance > minZoomDistance)
        {
            transform.position += directionToTarget * zoomSpeed * Time.deltaTime;
        }
        if (Input.GetKey(KeyCode.S))
        {
            transform.position -= directionToTarget * zoomSpeed * Time.deltaTime;
        }

        ApplyAngleConstraintAndLookAtTarget();
    }

    // --- 新しい外部から呼び出されるメソッド群 ---

    /// <summary>
    /// キーバインドモードでのカメラ移動を適用します。
    /// 各speed値は、CameraViewスクリプトの基本速度に乗算される係数です（例: 0.0〜1.0）。
    /// </summary>
    public void ApplyKeyMovement(float w_speed, float a_speed, float s_speed, float d_speed, float q_speed, float e_speed)
    {
        isExternalControl = true;
        isFollowing = false;
        isMovingToVectorPosition = false; // 他のモードが有効になったらVector移動を停止
        
        if (currentTargetTransform == null) return;

        Vector3 directionToTarget = (currentTargetTransform.position - this.transform.position).normalized;
        float currentDistance = Vector3.Distance(this.transform.position, currentTargetTransform.position);

        // 各キーの速度係数を適用
        if (w_speed > 0 && currentDistance > minZoomDistance) { transform.position += directionToTarget * (zoomSpeed * w_speed) * Time.deltaTime; }
        if (s_speed > 0) { transform.position -= directionToTarget * (zoomSpeed * s_speed) * Time.deltaTime; }
        if (a_speed > 0) { transform.RotateAround(currentTargetTransform.position, Vector3.up, -(rotationSpeed * a_speed) * Time.deltaTime); }
        if (d_speed > 0) { transform.RotateAround(currentTargetTransform.position, Vector3.up, (rotationSpeed * d_speed) * Time.deltaTime); }
        if (q_speed > 0) { transform.RotateAround(currentTargetTransform.position, this.transform.right, -(rotationSpeed * q_speed) * Time.deltaTime); }
        if (e_speed > 0) { transform.RotateAround(currentTargetTransform.position, this.transform.right, (rotationSpeed * e_speed) * Time.deltaTime); }

        ApplyAngleConstraintAndLookAtTarget();
    }

    /// <summary>
    /// カメラが追従するターゲットを設定します。
    /// </summary>
    public void SetFollowTarget(Transform target)
    {
        currentTargetTransform = target;
    }

    /// <summary>
    /// Followモードを有効にします。
    /// </summary>
    public void EnableFollowMode()
    {
        isExternalControl = true;
        isFollowing = true;
        isMovingToVectorPosition = false; // 他のモードが有効になったらVector移動を停止
    }

    /// <summary>
    /// Followモードを無効にします。
    /// </summary>
    public void DisableFollowMode()
    {
        isFollowing = false;
    }

    /// <summary>
    /// 全ての外部制御モード（キーバインド、Follow）を無効にし、Vectorモードへの移動を開始します。
    /// </summary>
    /// <param name="targetPosition">目標とするワールド座標 (Vector3)</param>
    /// <param name="speedFactor">移動速度係数 (0.0: 超ゆっくり - 1.0: 瞬間移動に近い)</param>
    public void MoveToVectorPosition(Vector3 targetPosition, float speedFactor)
    {
        isExternalControl = true;
        isFollowing = false;
        isMovingToVectorPosition = true; // Vectorモードでの移動を有効化
        Debug.Log($"[VectorMove] MoveToVectorPosition called. Setting isMovingToVectorPosition to TRUE. Current: {isMovingToVectorPosition}"); // 追加

        vectorTargetPosition = targetPosition;
        currentVectorMoveSpeedFactor = speedFactor;
        
        Debug.Log($"CameraView: Starting vector move to {targetPosition} with factor {speedFactor}");
    }

    /// <summary>
    /// 全ての外部制御モードを無効にします（主に他のモードへの切り替え時に呼び出され、補間も停止）。
    /// </summary>
    public void DisableAllModes()
    {
        isExternalControl = true;
        isFollowing = false;
        isMovingToVectorPosition = false; // Vectorモードでの移動を停止
    }

    // --- 補助メソッド ---

    /// <summary>
    /// 縦回転の角度を制限し、常にターゲットを見るように調整します。
    /// </summary>
    private void ApplyAngleConstraintAndLookAtTarget()
    {
        if (currentTargetTransform == null) return;

        Vector3 currentEulerAngles = transform.eulerAngles;
        float currentXAngle = currentEulerAngles.x;
        if (currentXAngle > 180) { currentXAngle -= 360; }
        float clampedXAngle = Mathf.Clamp(currentXAngle, minVerticalAngle, maxVerticalAngle);
        transform.rotation = Quaternion.Euler(clampedXAngle, currentEulerAngles.y, 0);

        Vector3 finalDirectionToTarget = (currentTargetTransform.position - this.transform.position).normalized;
        Quaternion targetRotation = Quaternion.LookRotation(finalDirectionToTarget, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * lookAtLerpSpeed);
    }
    
    /// <summary>
    /// Followモード時のカメラ移動ロジック。
    /// </summary>
    private void ApplyFollowMovement()
    {
        if (currentTargetTransform == null || Camera.main == null) return;

        Vector3 viewportPoint = Camera.main.WorldToViewportPoint(currentTargetTransform.position);
        bool isTargetInFrontOfCamera = (viewportPoint.z > 0);
        bool isTargetVisibleInScreen = (viewportPoint.x >= 0 && viewportPoint.x <= 1 &&
                                        viewportPoint.y >= 0 && viewportPoint.y <= 1);

        bool needsToStartFollowing = false;
        if (!isTargetInFrontOfCamera || 
            viewportPoint.x < followMargin || viewportPoint.x > 1 - followMargin ||
            viewportPoint.y < followMargin || viewportPoint.y > 1 - followMargin)
        {
            needsToStartFollowing = true;
        }

        bool needsToStopFollowing = false;
        if (isTargetInFrontOfCamera && isTargetVisibleInScreen && 
            viewportPoint.x >= stopFollowMargin && viewportPoint.x <= 1 - stopFollowMargin &&
            viewportPoint.y >= stopFollowMargin && viewportPoint.y <= 1 - stopFollowMargin)
        {
            needsToStopFollowing = true;
        }

        bool previousIsFollowing = isFollowing;

        if (needsToStartFollowing) { isFollowing = true; }
        else if (previousIsFollowing && needsToStopFollowing) { isFollowing = false; }

        if (isFollowing)
        {
            Vector3 targetForward = currentTargetTransform.forward;
            Vector3 desiredPosition = currentTargetTransform.position - targetForward * desiredFollowDistance + Vector3.up * desiredHeightOffset;

            transform.position = Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * lookAtLerpSpeed);
            
            Vector3 directionToTarget = (currentTargetTransform.position - this.transform.position).normalized;
            Quaternion targetRot = Quaternion.LookRotation(directionToTarget, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * lookAtLerpSpeed);
        }

        ApplyAngleConstraintAndLookAtTarget();
    }

    /// <summary>
    /// Vectorモードでカメラを指定位置へ滑らかに移動させるロジック。
    /// LateUpdateで呼び出され、目標位置に到達するまで移動を継続します。
    /// </summary>
    private void ApplyVectorPositionMovement()
    {
        // 目標位置と現在の位置がほぼ同じであれば移動を停止
        if (Vector3.Distance(transform.position, vectorTargetPosition) < 0.01f)
        {
            transform.position = vectorTargetPosition; // ピッタリ合わせる
            isMovingToVectorPosition = false; // 移動完了
            Debug.Log("CameraView: Reached vector target position.");
            return;
        }

        // Lerpの補間係数を計算
        float lerpFactor = currentVectorMoveSpeedFactor; // Pythonから受け取った0.0〜1.0の値

        Debug.Log($"[VectorMove] Current Pos: {transform.position}, Target Pos: {vectorTargetPosition}"); // 位置の変化を確認
        Debug.Log($"[VectorMove] Raw Lerp Factor (Python): {lerpFactor}"); // Pythonからの生の値

        // Time.deltaTimeを考慮した補間係数に変換
        // vectorMoveBaseSpeed を乗算することで、Inspectorから全体の移動速度を調整できるようにする
        float adjustedLerpFactor = Mathf.Clamp01(lerpFactor * vectorMoveBaseSpeed * Time.deltaTime);

        Debug.Log($"[VectorMove] vectorMoveBaseSpeed: {vectorMoveBaseSpeed}, Time.deltaTime: {Time.deltaTime}"); // 補間計算の入力値
        Debug.Log($"[VectorMove] Adjusted Lerp Factor: {adjustedLerpFactor}"); // 実際にLerpに使われる値

        // 線形補間を使って滑らかに移動
        transform.position = Vector3.Lerp(transform.position, vectorTargetPosition, adjustedLerpFactor);
        
        Debug.Log($"[VectorMove] New Pos after Lerp: {transform.position}"); // Lerp後の新しい位置
    }
}