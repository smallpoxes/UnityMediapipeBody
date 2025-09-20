using UnityEngine;

public class SetRenderQueueScript : MonoBehaviour
{
    // インスペクターで設定するターゲットのマテリアル
    public Material targetMaterial;

    // 設定したいレンダーキュー値
    // Geometry (不透明): 2000
    // Transparent (半透明): 3000
    // Overlay (UIなど一番上): 4000
    [Tooltip("描画順序のキュー値。高いほど後に描画されます。")]
    public int desiredRenderQueue = 3001; 

    void Start()
    {
        // targetMaterialが設定されていない場合、このGameObjectのRendererからマテリアルを取得
        if (targetMaterial == null)
        {
            Renderer renderer = GetComponent<Renderer>();
            if (renderer != null)
            {
                targetMaterial = renderer.material;
            }
            else
            {
                Debug.LogError("ターゲットマテリアルもRendererも見つかりません。", this);
                return;
            }
        }

        // マテリアルのレンダーキューを設定
        SetMaterialRenderQueue();
    }

    // インスペクターで値を変更したときにすぐに反映されるように (開発時のみ有効)
    void OnValidate()
    {
        if (targetMaterial != null)
        {
            SetMaterialRenderQueue();
        }
    }

    private void SetMaterialRenderQueue()
    {
        if (targetMaterial != null)
        {
            // ここでレンダーキューを設定します
            targetMaterial.renderQueue = desiredRenderQueue;
            //Debug.Log($"マテリアル '{targetMaterial.name}' のレンダーキューを {desiredRenderQueue} に設定しました。");
        }
    }

    // 外部からレンダーキューを変更したい場合に呼び出すメソッド
    public void ChangeRenderQueue(int newQueueValue)
    {
        desiredRenderQueue = newQueueValue;
        SetMaterialRenderQueue();
    }
}