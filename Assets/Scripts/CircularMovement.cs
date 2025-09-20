using UnityEngine;

public class CircularMovement : MonoBehaviour
{
    [SerializeField] private float radius = 5.0f;     // 円の半径
    [SerializeField] private float speed = 100.0f;    // 回転速度 (度/秒)

    private Vector3 centerPoint;
    private float angle = 0.0f;

    void Start()
    {
        // オブジェクトの初期位置を中心点として設定
        centerPoint = transform.position;
    }

    void Update()
    {
        // 角度を時間と共に増加させる
        angle += speed * Time.deltaTime;

        // 角度を360度でループさせる
        if (angle > 360)
        {
            angle = 0;
        }

        // ラジアンに変換
        float rad = angle * Mathf.Deg2Rad;

        // sinとcosを使って円周上の座標を計算
        float x = Mathf.Cos(rad) * radius;
        float z = Mathf.Sin(rad) * radius;

        // 計算した座標を中心点に加算し、オブジェクトの位置を更新
        transform.position = centerPoint + new Vector3(x, 0, z);
    }
}