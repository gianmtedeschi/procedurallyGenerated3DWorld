Shader "Custom/StandardVertex" {
    Properties{
        _Color("Color", Color) = (0.5,0.5,0.5,1)
        _ColorGround("ColorGround", Color) = (0.5,0.5,0.5,1)

        _OcclusionColor("OcclusionColor", Color) = (0,0,0,1)
        _OcclusionPow("OcclusionPow", Range(1, 10)) = 1
        _OcclusionIntensity("OcclusionIntensity", Range(0, 10))=1

        _HighlightColor("HighlightColor", Color) = (1,1,1,1)
        _HighlightPow("HighlightPow", Range(1, 10)) = 1
        _HighlightIntensity("HighlightIntensity", Range(0, 10)) = 1

        _OcclusionColorGround("OcclusionColorGround", Color) = (0,0,0,1)
        _OcclusionPowGround("OcclusionPowGround", Range(1, 10)) = 1
        _OcclusionIntensityGround("OcclusionIntensityGround", Range(0, 10)) = 1

        _HighlightColorGround("HighlightColorGround", Color) = (1,1,1,1)
        _HighlightPowGround("HighlightPowGround", Range(1, 10)) = 1
        _HighlightIntensityGround("HighlightIntensityGround", Range(0, 10)) = 1

        _MainTex("Albedo (RGB)", 2D) = "white" {}
        _Glossiness("Smoothness", Range(0,1)) = 0.5
        _Metallic("Metallic", Range(0,1)) = 0.0
    }
        SubShader{
            Tags { "RenderType" = "Opaque" }
            LOD 200

            CGPROGRAM
            #pragma surface surf Standard vertex:vert fullforwardshadows
            #pragma target 3.0
            struct Input {
                float2 uv_MainTex;
                float3 vertexColor; // Vertex color stored here by vert() method
            };

            struct v2f {
              float4 pos : SV_POSITION;
              fixed4 color : COLOR;
            };

            void vert(inout appdata_full v, out Input o)
            {
                UNITY_INITIALIZE_OUTPUT(Input,o);
                o.vertexColor = v.color; // Save the Vertex Color in the Input for the surf() method
            }

            sampler2D _MainTex;

            half _Glossiness;
            half _Metallic;

            float4 _Color;
            float4 _OcclusionColor;
            float _OcclusionPow;
            float _OcclusionIntensity;
            float4 _HighlightColor;
            float _HighlightPow;
            float _HighlightIntensity;

            float4 _ColorGround;
            float4 _OcclusionColorGround;
            float _OcclusionPowGround;
            float _OcclusionIntensityGround;
            float4 _HighlightColorGround;
            float _HighlightPowGround;
            float _HighlightIntensityGround;

            float4 GetColor(float value, float4 baseColor,
                float occlusionPow, float occlusionIntensity, float4 occlusionColor,
                float highlightPow, float highlightIntensity, float4 highlightColor)
            {
                float occlusionFac = saturate(pow(value, occlusionPow) * occlusionIntensity);
                float highlightFac = saturate(pow((1 - value), highlightPow) * highlightIntensity);

                fixed4 col = (occlusionColor * occlusionFac) + (1 - occlusionFac) * baseColor;
                col = (highlightFac * highlightColor) + (1 - highlightFac) * col;

                return col;
            }

            void surf(Input IN, inout SurfaceOutputStandard o)
            {
                
                float4 col = GetColor(
                    IN.vertexColor.r,
                    IN.vertexColor.b < 0.5f ? _Color : _ColorGround,
                    IN.vertexColor.b < 0.5f ? _OcclusionPow : _OcclusionPowGround,
                    IN.vertexColor.b < 0.5f ? _OcclusionIntensity : _OcclusionIntensityGround,
                    IN.vertexColor.b < 0.5f ? _OcclusionColor : _OcclusionColorGround,
                    IN.vertexColor.b < 0.5f ? _HighlightPow : _HighlightPowGround,
                    IN.vertexColor.b < 0.5f ? _HighlightIntensity : _HighlightIntensityGround,
                    IN.vertexColor.b < 0.5f ? _HighlightColor : _HighlightColorGround);
                o.Albedo = col; // Combine normal color with the vertex color
                // Metallic and smoothness come from slider variables
                o.Metallic = _Metallic;
                o.Smoothness = _Glossiness;
                o.Alpha = 1;
            }
            ENDCG
        }
            FallBack "Diffuse"
}