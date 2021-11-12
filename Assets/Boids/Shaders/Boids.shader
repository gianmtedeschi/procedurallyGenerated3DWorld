Shader "Custom/Boids" {

	Properties{
		_Scale("Scale", Range(0.01, 10))=0.1
		_EmissionStrength("Emission", Range(0.01, 10)) = 0.1
		[HDR] _ColorX("ColorX", Color) = (0.5, 0.5, 0.5, 1.0)
		[HDR] _ColorY("ColorY", Color) = (0.5, 0.5, 0.5, 1.0)
		[HDR] _ColorZ("ColorZ", Color) = (0.5, 0.5, 0.5, 1.0)
	}

		SubShader{
			CGPROGRAM
			#pragma instancing_options assumeuniformscaling procedural:ConfigureProcedural
			#pragma surface ConfigureSurface Standard fullforwardshadows addshadow			
			#pragma target 4.5

			struct Input {
			float3 worldPos;
		};

			#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
			StructuredBuffer<float3> _Positions;
			StructuredBuffer<float3> _Directions;
			StructuredBuffer<float3> _PrevDirections;
			#endif

			float _Scale;
			float _EmissionStrength;
			float3 _ColorX;
			float3 _ColorY;
			float3 _ColorZ;

			void ConfigureProcedural() {
				#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
					float3 position = _Positions[unity_InstanceID];
					float3 direction = _Directions[unity_InstanceID];
					float3 prevDirection = _PrevDirections[unity_InstanceID];
					float3 forwardObject = float3(0.0, 1.0, 0.0);
					
					float3 x = normalize(cross(forwardObject, direction));
					float thetha = acos(dot(forwardObject, direction));
					
					float3x3 id3 =
					{
						1.0, 0.0, 0.0,
						0.0, 1.0, 0.0,
						0.0, 0.0, 1.0
					};

					float4x4 id4 =
					{
						1.0, 0.0, 0.0, 0.0,
						0.0, 1.0, 0.0, 0.0,
						0.0, 0.0, 1.0, 0.0,
						0.0, 0.0, 0.0, 1.0
					};

					float3x3 a3 =
					{
						0.0, -x.z, x.y,
						x.z, 0.0, -x.x,
						-x.y, x.x, 0.0
					};

					float3x3 r3 = id3 + sin(thetha) * a3 + (1 - cos(thetha)) *mul(a3, a3);

					float4x4 rot4 =
					{
						r3._m00_m01_m02, 0.0,
						r3._m10_m11_m12, 0.0,
						r3._m20_m21_m22, 0.0,
						0.0, 0.0, 0.0,   1.0
					};

					float4x4 scale4 =
					{
						_Scale, 0.0, 0.0, 0.0,
						0.0, _Scale, 0.0, 0.0,
						0.0, 0.0, _Scale, 0.0,
						0.0, 0.0, 0.0,    1.0
					};

					float4x4 trasl4 =
					{
						1.0, 0.0, 0.0, position.x,
						0.0, 1.0, 0.0, position.y,
						0.0, 0.0, 1.0, position.z,
						0.0, 0.0, 0.0, 1.0
					};

					unity_ObjectToWorld =  mul(mul(trasl4, scale4), rot4);/** scale4 * trasl4*/  /*rot4 **/;

				#endif
			}

			void ConfigureSurface(Input input, inout SurfaceOutputStandard surface)
			{
#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
				float3 direction = _Directions[unity_InstanceID];
				float3 prevDirection = _PrevDirections[unity_InstanceID];
				float3 col3 = _ColorX * abs(direction.x) + _ColorY * abs(direction.y) + _ColorZ * abs(direction.z);
				col3 /= 3.0;
				float emission = pow(length(direction - prevDirection), 1.5f) *10.0f* _EmissionStrength;
#ifndef UNITY_COLORSPACE_GAMMA
				col3 = LinearToGammaSpace(col3);
#endif
				// apply intensity exposure
				col3 *= pow(2.0, emission);
				// if not using gamma color space, convert back to linear
#ifndef UNITY_COLORSPACE_GAMMA
				col3 = GammaToLinearSpace(col3);
#endif
				surface.Albedo = col3;
				surface.Alpha = 1.0f;
#endif
			}

			ENDCG

	}
		FallBack "Diffuse"
	
}