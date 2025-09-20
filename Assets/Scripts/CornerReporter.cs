using UnityEngine;

/// <summary>
/// アタッチされたPlaneオブジェクトの四隅のワールド座標を取得し、Inspectorに表示するクラス。
/// </summary>
public class CornerReporter : MonoBehaviour
{
    // Inspectorに表示するための、ワールド座標を格納する配列
    [Header("計算された四隅のワールド座標")]
    public Vector3[] worldCorners = new Vector3[4];

    // ゲーム開始時に一度だけ実行される
    void Awake()
    {
        CalculateCorners();
    }

    /// <summary>
    /// 四隅の座標を計算してworldCorners配列に格納します。
    /// </summary>
    [ContextMenu("四隅の座標を再計算")] // Inspectorでこの関数を直接呼び出せるようにする
    public void CalculateCorners()
    {
        // 自分自身が持っているMeshFilterコンポーネントを取得
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            Debug.LogError("MeshFilterまたはメッシュが見つかりません。", this);
            return;
        }

        // メッシュのバウンディングボックス（境界となる箱）を取得
        Bounds bounds = meshFilter.sharedMesh.bounds;

        // 1. ローカル座標での四隅の点を定義する
        // A standard plane is 10x10 units, so its local bounds are from -5 to 5.
        Vector3[] localCorners = new Vector3[4];
        localCorners[0] = new Vector3(bounds.min.x, 0, bounds.max.y); // 左上 (Left-Top)
        localCorners[1] = new Vector3(bounds.max.x, 0, bounds.max.y); // 右上 (Right-Top)
        localCorners[2] = new Vector3(bounds.min.x, 0, bounds.min.y); // 左下 (Left-Bottom)
        localCorners[3] = new Vector3(bounds.max.x, 0, bounds.min.y); // 右下 (Right-Bottom)

        // 2. 各ローカル座標をワールド座標に変換する
        for (int i = 0; i < localCorners.Length; i++)
        {
            // TransformPointは、ローカル座標をワールド座標に変換するUnityの標準機能
            worldCorners[i] = transform.TransformPoint(localCorners[i]);
        }

        // 確認のためにコンソールにも出力する
        // Debug.Log($"Corner 0 (左上): {worldCorners[0]}");
        // Debug.Log($"Corner 1 (右上): {worldCorners[1]}");
        // Debug.Log($"Corner 2 (左下): {worldCorners[2]}");
        // Debug.Log($"Corner 3 (右下): {worldCorners[3]}");
    }
}