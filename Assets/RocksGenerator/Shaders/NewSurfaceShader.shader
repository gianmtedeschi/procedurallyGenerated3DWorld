Shader "Custom/StandardVertex" {
    Properties{
        _Color("Color", Color) = (0.5,0.5,0.5,1)

        _OcclusionColor("OcclusionColor", Color) = (0,0,0,1)
        _OcclusionPow("OcclusionPow", Range(1, 10)) = 1
        _OcclusionIntensity("OcclusionIntensity", Range(0, 10))=1

        _HighlightColor("HighlightColor", Color) = (1,1,1,1)
        _HighlightPow("HighlightPow", Range(1, 10)) = 1
        _HighlightIntensity("HighlightIntensity", Range(0, 10)) = 1

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
            fixed4 _Color;
            fixed4 _OcclusionColor;
            float _OcclusionPow;
            float _OcclusionIntensity;

            fixed4 _HighlightColor;
            float _HighlightPow;
            float _HighlightIntensity;
            void surf(Input IN, inout SurfaceOutputStandard o)
            {
                // Albedo comes from a texture tinted by color
                float occlusionFac = saturate(pow(IN.vertexColor.r, _OcclusionPow) * _OcclusionIntensity);
                float highlightFac = saturate(pow((1-IN.vertexColor.r), _HighlightPow) * _HighlightIntensity);

                fixed4 col= (_OcclusionColor * occlusionFac) + (1 - occlusionFac) * _Color;
                col= (highlightFac * _HighlightColor) + (1 - highlightFac) * col;
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