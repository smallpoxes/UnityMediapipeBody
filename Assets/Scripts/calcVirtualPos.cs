using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class calcVirtualPos : MonoBehaviour
{
    public GameObject target1; // ターゲットオブジェクト（アバターの頭部など）
    public GameObject target2; // ターゲットオブジェクト（アバターの腰など）
    public float groundOffset = 0.1f; // 地面からのオフセット
    public Vector3 targetPosition1; // ターゲットのTransform
    public Vector3 targetPosition2; // ターゲットのTransform
    public Vector3 virtualPosition; // 計算された仮想位置
    // Start is called before the first frame update
    void Start()
    {
        targetPosition1 = target1.transform.position;
        targetPosition2 = target2.transform.position;
    }

    // Update is called once per frame
    void Update()
    {
        // ターゲットの位置を更新
        targetPosition1 = target1.transform.position;
        targetPosition2 = target2.transform.position;

        // 仮想位置を計算（例：ターゲットの中間点）
        virtualPosition = (targetPosition1 + targetPosition2) / 2;
        virtualPosition.y = virtualPosition.y - groundOffset;

        // 仮想位置をオブジェクトの位置として設定
        transform.position = virtualPosition;
        
    }
}
