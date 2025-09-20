Shader "Custom/DistanceColorBodyPart"
{
    Properties
    {
        _Color ("Base Color", Color) = (1,1,1,1) // 元のオブジェクトの色（ピンクなど）
        _NearBrightness ("Near Brightness", Range(0, 2)) = 1.0 // 近い時の明るさの倍率（1.0で元の明るさ）
        _FarBrightness ("Far Brightness", Range(0, 1)) = 0.2 // 遠い時の明るさの倍率（0.0で真っ黒、1.0で元の明るさ）
        
        // --- 新しいプロパティ：アルファ値の調整 ---
        _NearAlpha ("Near Alpha", Range(0, 1)) = 1.0 // 近い時のアルファ値（1.0で完全不透明）
        _FarAlpha ("Far Alpha", Range(0, 1)) = 0.7 // 遠い時のアルファ値（0.0で完全透明）
        // ------------------------------------------
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Transparent" }
        LOD 100

        Blend SrcAlpha OneMinusSrcAlpha // 透明度を有効にする
        ZWrite Off // 深度書き込みをオフにする（透過表現用）
        ZTest LEqual // デフォルトの深度テスト（手前にあるものを描画）

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc" 

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD0; // ワールド座標を渡す
            };

            fixed4 _Color; 
            float _NearBrightness;
            float _FarBrightness;
            float _NearAlpha; // 新しいプロパティ
            float _FarAlpha;  // 新しいプロパティ

            float _BodyMinDistance;
            float _BodyMaxDistance;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float currentPixelDistance = distance(i.worldPos, _WorldSpaceCameraPos);

                float distanceRange = _BodyMaxDistance - _BodyMinDistance;
                float t;
                if (distanceRange > 0.001)
                {
                    t = saturate((currentPixelDistance - _BodyMinDistance) / distanceRange);
                }
                else
                {
                    t = 0; 
                }

                // 明るさの計算
                float currentBrightness = lerp(_NearBrightness, _FarBrightness, t);
                fixed4 finalColor = _Color * currentBrightness;

                // --- 新しい機能：アルファ値の計算と適用 ---
                // t = 0 のときに _NearAlpha (近い時のアルファ値)
                // t = 1 のときに _FarAlpha (遠い時のアルファ値)
                float currentAlpha = lerp(_NearAlpha, _FarAlpha, t);
                finalColor.a = currentAlpha; // 計算したアルファ値を適用
                // ---------------------------------------------

                return finalColor;
            }
            ENDCG
        }
    }
}