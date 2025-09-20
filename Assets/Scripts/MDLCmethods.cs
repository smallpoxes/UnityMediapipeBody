using UnityEngine;

public static class MDLCmethods
{
    public static void RotateLights(ref float redrotate, ref float yellowrotate, ref float bluerotate, Transform[] lightTransforms, float rotationSpeed)
    {
        redrotate = (redrotate + rotationSpeed * Time.deltaTime) % 360.0f;
        yellowrotate = (yellowrotate + rotationSpeed * Time.deltaTime) % 360.0f;
        bluerotate = (bluerotate + rotationSpeed * Time.deltaTime) % 360.0f;
        Quaternion redRotation = Quaternion.Euler(redrotate, 0, 0f);
        Quaternion blueRotation = Quaternion.Euler(bluerotate, 120f, 0f);
        Quaternion yellowRotation = Quaternion.Euler(yellowrotate, 240f, 0f);
        if (lightTransforms == null || lightTransforms.Length == 0) return;

        // 時間経過でY軸周りに回転させる例
        // transform.Rotate(Vector3.up * rotationSpeed * Time.deltaTime); // これだと親オブジェクトが回転する

        lightTransforms[0].localRotation = redRotation;
        lightTransforms[1].localRotation = blueRotation;
        lightTransforms[2].localRotation = yellowRotation;

    }


    public static void ProcessVariousRotation(bool Switch, Transform[] lightTransforms,
                                 Vector3 baseAxis, int pBase, float dirFeature,
                                 float smoothTime, float rotationSpeed, float featureValue, ref Vector3 axisSmoothed)
    {
        for (int i = 0; i < lightTransforms.Length; i++)
        {
            // 自分の permutation を決定（3体で必ず違う組み合わせになる）
            int perm = (pBase + i) % 6;
            var targetAxis = Permute(baseAxis, perm).normalized;


            float rotationAmount = (dirFeature > featureValue ? rotationSpeed : -rotationSpeed) * Time.deltaTime;
            //dir = rotationSpeed;

            // スムーズ
            axisSmoothed = Vector3.Slerp(
                axisSmoothed.sqrMagnitude > 1e-6f ? axisSmoothed.normalized : targetAxis,
                targetAxis,
                1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.0001f, smoothTime))
            );


            lightTransforms[i].Rotate(axisSmoothed, rotationAmount, Space.Self);
        }
    }

    public static Vector3 Permute(Vector3 v, int permId)
    {
        switch (permId % 6)
        {
            case 0: return new Vector3(v.x, v.y, v.z);
            case 1: return new Vector3(v.x, v.z, v.y);
            case 2: return new Vector3(v.y, v.x, v.z);
            case 3: return new Vector3(v.y, v.z, v.x);
            case 4: return new Vector3(v.z, v.x, v.y);
            default: return new Vector3(v.z, v.y, v.x);
        }
    }
}