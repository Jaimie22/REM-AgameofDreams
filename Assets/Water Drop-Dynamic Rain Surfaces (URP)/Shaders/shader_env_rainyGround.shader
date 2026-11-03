Shader "Water Drop-Dynamic Rain Surfaces (URP)/RainyGround"
{
    Properties
    {
        // Basic ground properties
        _MainTex ("Texture", 2D) = "white" {}
        _Tint ("Tint", Color) = (1,1,1,1)
        _NormalTex ("Normal Map", 2D) = "normal" {}
        _NormalScale ("Normal Scale", Range(0,20)) = 1
        
        // Ground height and puddle settings
        _HeightTex ("Height Texture", 2D) = "" {}
        _HeightTexContrast ("Height Texture Contrast", Range(0, 1.0)) = 0.8
        _Blur("Blur", Range(0,20)) = 1.0  
        _PuddleMask ("Puddle Mask", 2D) = "" {} // Mask defining where water will accumulate
        
        // Physically Based Rendering (PBR) properties
        _Metallic ("Metallic", Range(0,1)) = 0
        _Smoothness ("Smoothness", Range(0,1)) = 0.5
        _LUT("LUT", 2D) = "white" {} // Look-Up Table for lighting calculations

        // Water properties
        _WetLevel("Wet Level", Range(0,1)) = 0 // How wet the ground is (0: Dry, 1: Fully wet)
        _WaterTint ("Water Tint", color) = (1, 1, 1, 1) 
        _WaveMap ("Wave map", 2D) = "bump" {} // Normal map for water surface ripples/waves
        _WaveMapScale ("Wave Map Scale", Range(0,20)) = 1
        _WaveXSpeed ("Wave Horizontal Speed", Range(-0.1, 0.1)) = 0.02
        _WaveYSpeed ("Wave Vertical Speed", Range(-0.1, 0.1)) = 0.02
        _Distortion ("Refraction Distortion", float) = 80 // Amount of refraction when looking through water
        _Gloss ("Water Glossiness", float) = 300
        
        // Raindrop ripples
        _RippleTex ("Ripple Tex", 2D) = "" {}
        _RippleIntens ("Ripple Intensity", Range(0, 10)) = 1

        _WaterSmoothness ("Water Smoothness", Range(0, 1)) = 0.8
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        // Shared functions used across all Passes are defined here
        CGINCLUDE

        #include "UnityCG.cginc"

         // Function to calculate the ripples created by raindrops hitting the water
         float3 ComputeRipple(sampler2D _RippleTex ,float2 uv, float time)
            {
                float4 ripple = tex2D(_RippleTex, uv);
                // Convert RGB (0,1) values to Normal Map (-1,1) format
                ripple.yz = ripple.yz * 2 -1;   
                
                // ripple.w stores a random value so different drops fall at different times
                float dropFrac = frac(ripple.w + time);  
                // 2D texture ripple.x represents the strength of the wave
                // The wave expands outward over time
                float timeFrac = dropFrac - 1.0 + ripple.x;
                // Attenuation (fading) effect over time
                float dropFactor = 1 - saturate(dropFrac);
                // clamp - Adjusts the ratio between the sine wave and the calm state
                float finalFactor = dropFactor * ripple.x * sin( clamp(timeFrac * 9, 0, 4) * UNITY_PI );

                return float3(ripple.yz * finalFactor, 1);
            }

        // Pseudo-random number generator returning a value between 0 and 1
        float N21(float2 p) 
        {
            p = frac(p*float2(123.34, 345.34));
            p += dot(p, p+34.345);

            return frac(p.x*p.y);
        }

        // Blur function (Used to soften the edges of the water puddles)
        fixed3 blurTex(float2 uv, sampler2D tex, float4 tex_TexelSize, float blur)
        {
            const int sampleNum = 16;
            fixed a = N21(uv)*6.28; // Start from a random angle
            fixed3 col = 0;
            // Sample surrounding pixels 16 times and average them to create a blur
            for (int n = 0; n < sampleNum; n++)
            {
                float2 offs = float2(sin(a), cos(a))* blur;
                offs *= n * tex_TexelSize.xy;
                col += tex2D( tex, uv + offs).rgb;
                a++;
            }
            col /= sampleNum;
            return col;
        }
        ENDCG

        Pass    // GROUND PASS ////////////////////////////////////////////////////
        {
            Tags { "LightMode"="UniversalForward" "Queue"="Opaque"}
            // ColorMask 0

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_fwdbase

            #include "UnityCG.cginc"
            #include "UnityStandardBRDF.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
            };

            // Data passed from Vertex shader to Fragment shader
            struct v2f
            {
                float4 uv : TEXCOORD0; // .xy: MainTex, .zw: PuddleMask
                float2 uv_ripple : TEXCOORD4;
                float4 pos : SV_POSITION;
                // TBN Matrix (To convert Normal map to world coordinates)
                float4 TBN0 : TEXCOORD1;
                float4 TBN1 : TEXCOORD2;
                float4 TBN2 : TEXCOORD3;

            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            fixed4 _Tint;
            fixed _Metallic;
            fixed _Smoothness;
            sampler2D _LUT;
            sampler2D _NormalTex;
            float _NormalScale;
            float _WetLevel;
            sampler2D _HeightTex;
            float4 _HeightTex_TexelSize;
            sampler2D _PuddleMask;
            float4 _PuddleMask_ST;
            float4 _WaterTint;
            float _HeightTexContrast;
            sampler2D _WaveMap;
            float _WaveXSpeed;
            float _WaveYSpeed;
            float _WaveMapScale;
            float _Distortion;
            sampler2D _RippleTex;
            float4 _RippleTex_ST;
            float _RippleIntens;
            float _Blur;

            v2f vert (appdata v)
            {
                v2f o;

                o.pos = UnityObjectToClipPos(v.vertex);
                // Setup texture coordinates (Tiling/Offset)
                o.uv.xy = TRANSFORM_TEX(v.uv, _MainTex);
                o.uv.zw = TRANSFORM_TEX(v.uv, _PuddleMask);
                o.uv_ripple = TRANSFORM_TEX(v.uv, _RippleTex);
                
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex);

                // Construct the TBN Matrix (Transition from Tangent Space to World Space)
                float3 worldNormal = UnityObjectToWorldNormal(v.normal);
                float3 worldTangent = UnityObjectToWorldDir(v.tangent.xyz);
                float3 worldBitangent = cross(worldNormal, worldTangent) * v.tangent.w;

                // Pack TBN data and World Position (worldPos) into the same variables (for optimization)
                o.TBN0 = float4(worldTangent.x, worldBitangent.x, worldNormal.x,worldPos.x);
                o.TBN1 = float4(worldTangent.y, worldBitangent.y, worldNormal.y,worldPos.y);
                o.TBN2 = float4(worldTangent.z, worldBitangent.z, worldNormal.z,worldPos.z);

                return o;
            }

            // Physical formula to calculate Fresnel reflection (reflection amount based on viewing angle)
            float3 fresnelSchlickRoughness(float cosTheta, float3 F0, float roughness)
                    {
                        return F0 + (max(float3(1,1,1) * (1-roughness), F0)
                                - F0) * pow(1.0 - cosTheta, 5.0);
                    }

          

            fixed4 frag (v2f i) : SV_Target
            {
                
                // Vector preparations
                float3 worldPos = float3(i.TBN0.w, i.TBN1.w, i.TBN2.w);
                float3 worldLightDir = normalize(UnityWorldSpaceLightDir(worldPos));
                float3 worldViewDir = normalize(UnityWorldSpaceViewDir(worldPos));
                fixed3 lightCol = _LightColor0.rgb;
                float2 speed = _Time.y * float2(_WaveXSpeed, _WaveYSpeed); // Wave movement speed


                fixed puddleMask = tex2D(_PuddleMask, i.uv.zw);

                // Blur the ground height texture to soften it
                fixed3 groundHeight = blurTex
                ( i.uv.xy, _HeightTex, _HeightTex_TexelSize, _Blur);

                // Calculate puddle boundaries (Logic for water filling the cavities)
                float val = _HeightTexContrast / 2;
                fixed groundHeight_C = smoothstep(val, 1-val, groundHeight);
                puddleMask = max( 1-groundHeight_C, puddleMask );
                puddleMask = saturate(puddleMask - groundHeight * 0.1 ) ;     // Subtract to fill gaps with water
                puddleMask = puddleMask > (1-_WetLevel) ? puddleMask : 0; // Clip water amount based on WetLevel

                
                
                

                // Ground Normal Calculation (Bumps/Dents) 
                fixed3 tNormal = UnpackNormal(tex2D(_NormalTex, i.uv.xy));
                tNormal.xy *= _NormalScale;
                tNormal.xy *= lerp(1.0, 0.8, _WetLevel);    // Ground looks smoother/glossier as it gets wet
                tNormal.xy = lerp(tNormal.xy, 0.3, puddleMask); // Flatten ground roughness where there is water
                tNormal.z = sqrt(1 - saturate(dot(tNormal.xy, tNormal)));

                // Water Normal Calculation (Ripples)
                // Blend waves flowing in two different directions
                fixed3 normal1_T = UnpackNormal(tex2D(_WaveMap, i.uv.xy + speed)).rgb;
                fixed3 normal2_T = UnpackNormal(tex2D(_WaveMap, i.uv.xy - speed)).rgb;
                fixed3 normal_T = normalize(normal1_T + normal2_T);
                normal_T.xy *= _WaveMapScale;
                normal1_T.z = 1 - sqrt(saturate(dot(normal_T.xy, normal_T.xy)));

                // Blend Ground normals with Water normals based on the puddle mask
                tNormal = lerp(tNormal, normal_T, puddleMask);

                // Add raindrop ripple normals
                fixed3 rippleNormal = ComputeRipple(_RippleTex ,i.uv_ripple, _Time.z);
                tNormal.xy = tNormal.xy + rippleNormal.xy * _RippleIntens * puddleMask;

                // Convert final normal in Tangent Space to World Space
                float3 worldNormal = normalize( float3(
                dot(i.TBN0.xyz, tNormal),
                dot(i.TBN1.xyz, tNormal),
                dot(i.TBN2.xyz, tNormal)
                ) );

                // Base ground color and refraction effect
                float2 offset = normal_T.xy * _Distortion * _MainTex_TexelSize;
                float2 uv = i.uv + offset * puddleMask ; // Slightly distort the texture underneath where there is water
                fixed3 baseCol = tex2D(_MainTex, uv);
                baseCol *= lerp(1.0, 0.5 , _WetLevel); // Ground darkens as it gets wet (Crucial physical property)

                // Glossiness and Roughness setup (Preparation for PBR math)
                _Smoothness = saturate(_Smoothness + 0.5 * _WetLevel); // Wet ground is smoother
                float perceptualRoughness = 1 - _Smoothness;
                float roughness = perceptualRoughness * perceptualRoughness;
                float squareRoughness = roughness * roughness;

                              
                // Angle vectors required for lighting calculations (Half Vector, Dot Products)
                float3 halfVector = normalize(worldLightDir + worldViewDir);
                float nh = max(saturate(dot(worldNormal, halfVector)), 0.00001);
                float nl = max(saturate(dot(worldNormal, worldLightDir)), 0.00001);
                float nv = max(saturate(dot(worldNormal, worldViewDir)), 0.00001);
                float vh = max(saturate(dot(worldViewDir, halfVector)), 0.00001);

// Water's diffuse color ---------------------------------------------
                fixed waterDiffCol = lightCol * _WaterTint * nl * puddleMask * (1-groundHeight);
                
// Direct Light - Specular Calculation (GGX PBR Model) --------------------------------------------
                    // NDF - Normal Distribution Function (How light scatters based on surface roughness)
                        // Keep a little specular reflection even if roughness is 1
                        float lerpSquareRoughness = pow(lerp(0.002,1,roughness), 2); 
                        float D = lerpSquareRoughness
                        / (UNITY_PI * pow( (nh*nh * (lerpSquareRoughness-1) + 1), 2));
                    // Geometry Function - Micro-shadowing of light (Schlick-GGX)
                        // k is a remapping of roughness
                        float k_directLight = pow(squareRoughness + 1, 2) / 8;
                        float k_IBL = pow(squareRoughness, 2) / 8;
                        // Shadowing when light comes in
                        // Masking when light comes out
                        // Therefore G is calculated twice and multiplied
                        float G_in = nl / lerp(nl, 1, k_directLight);
                        float G_out = nv / lerp(nv, 1, k_directLight);
                        float G = G_in * G_out;
                    // Fresnel - Reflection increases at grazing angles (Unreal Engine formula used)
                        // F0: Base reflectivity of dielectric (non-metal) materials
                        // Or Unity's built-in unity_ColorSpaceDielectricSpec.rgb variable can be used
                        float3 F0 = unity_ColorSpaceDielectricSpec.rgb;
                        // Update F0 based on the Metallic value
                        F0 = lerp(F0, baseCol, _Metallic);
                        float3 F = F0 + (1-F0) * exp2((-5.55473*vh - 6.98316)*vh);
                    // PBR Specular BRDF Equation
                    float3 BRDF_DL_spec = (D * G * F * 0.25) / (nv * nl);
                        // Multiply by Pi to align with diffuse color
                    float3 specCol_DL = BRDF_DL_spec * lightCol * nl * UNITY_PI;
                    specCol_DL *= 1-puddleMask;     // Ground doesn't reflect its own specular in a puddle (Water does)

                // Direct Light - Diffuse (Lambert Model) ----------------------
                    // The remaining portion of unreflected light is the diffuse light
                    fixed kd_DL = (1-F) * (1-_Metallic);   
                    // Diffuse color (Incoming light * Base Color * Angle)
                    fixed3 diffCol_DL = kd_DL * baseCol * lightCol * nl;
                    
                    
                // Final Result for Direct Light (Specular + Diffuse)
                float3 col_DL = diffCol_DL + specCol_DL;

// Indirect Light (Ambient / IBL) - Specular ---------------------------------------------
                    // Take reflection sample from the environment map (Skybox/Reflection Probe)
                    float mip_roughness = perceptualRoughness * (1.7 - 0.7*perceptualRoughness);
                    half mip = mip_roughness * UNITY_SPECCUBE_LOD_STEPS;
                    float3 reflVec = reflect(-worldViewDir, worldNormal);
                    half4 rgbm = UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0, reflVec, mip);
                    float3 iblSpec = DecodeHDR(rgbm, unity_SpecCube0_HDR);

                    // BRDF calculation for ambient light
                    float2 envBRDF = tex2D(_LUT, float2(lerp(0, 0.99, nv), lerp(0, 0.99, roughness))).rg;
                        // Fresnel Schlick roughness function
                    float3 F_indirect = fresnelSchlickRoughness( max(nv,0), F0, roughness );
                    float3 specCol_IBL = iblSpec * (F_indirect * envBRDF.r + envBRDF.g);
                    specCol_IBL *= 1-puddleMask;     // Again, ground has no IBL specular reflection in a puddle


                // Indirect Light (Ambient) - Diffuse
                    float kd_IBL = (1 - F_indirect) * (1 - _Metallic);
                    // Get ambient light using Spherical Harmonics (SH)
                    float3 ambient_SH = ShadeSH9(float4(worldNormal, 1.0));
                    float3 ambient = 0.03 * baseCol; // Add a very slight minimum ambient lighting
                    half3 IBLCol = max(0, ambient_SH + ambient);
                    fixed3 diffCol_IBL = kd_IBL * baseCol * IBLCol;
                
                // Final Result for Indirect Light
                fixed3 col_IBL = specCol_IBL + diffCol_IBL + waterDiffCol;
                
                

                // FINAL OUTPUT COLOR (Direct Lights + Indirect Lights)
                fixed3 finalCol = col_DL +col_IBL;
                
                fixed4 col = 1;
                col.rgb =  finalCol;
                // col.rgb = puddleMask; // Test code that can be used during development to see the mask
                return col;
            }
            ENDCG
        }

        Pass    // PUDDLE PASS ////////////////////////////////////////////////////
        // This pass is completely transparent and renders water reflections on top of the ground
        {
            Tags {"LightMode"="UniversalForward" "Queue"="Transparent"}
            Blend SrcAlpha OneMinusSrcAlpha // Transparency blending mode
            ZWrite Off // Don't write water to the Z-Buffer so objects behind it don't break
            ColorMask 0

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_fwdbase

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 tangent : TANGENT;
                float3 normal : NORMAL;
            };

            // Same v2f structure as the Ground Pass, used here just for the water surface
            struct v2f
            {
                float4 uv : TEXCOORD0;
                float2 uv_ripple : TEXCOORD4;
                float4 pos : SV_POSITION;
                float4 TBN0 : TEXCOORD1;
                float4 TBN1 : TEXCOORD2;
                float4 TBN2 : TEXCOORD3;
            };

            fixed _WaveXSpeed;
            fixed _WaveYSpeed;
            sampler2D _WaveMap;
            float4 _WaveMap_ST;
            sampler2D _RefrTex;
            float4 _RefrTex_TexelSize;
            float _WaveMapScale;
            samplerCUBE _Cube;
            float4 _WaterTint;
            float _Gloss;
            float _WetLevel;
            sampler2D _PuddleMask;
            float4 _PuddleMask_ST;
            sampler2D _HeightTex;
            float _HeightTexContrast;
            
            float _WaterSmoothness;
            sampler2D _RippleTex;
            float _RippleIntens;
            

            v2f vert (appdata v)
            {
                v2f o;
                 o.pos = UnityObjectToClipPos(v.vertex);
                o.uv.xy = TRANSFORM_TEX(v.uv, _WaveMap);
                o.uv.zw = TRANSFORM_TEX(v.uv, _PuddleMask);

                float3 worldPos = mul(unity_ObjectToWorld, v.vertex);
                
                // (Commented out code) Water level could physically increase with _WetLevel
                //worldPos += lerp()

                // TBN Matrix calculation for water
                float3 worldNormal = UnityObjectToWorldNormal(v.normal);
                float3 worldTangent = UnityObjectToWorldDir(v.tangent.xyz);
                float3 worldBitangent = cross(worldNormal, worldTangent) * v.tangent.z;
                
                o.TBN0 = float4(worldTangent.x, worldBitangent.x, worldNormal.x, worldPos.x);
                o.TBN1 = float4(worldTangent.y, worldBitangent.y, worldNormal.y, worldPos.y);
                o.TBN2 = float4(worldTangent.z, worldBitangent.z, worldNormal.z, worldPos.z);

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 worldPos = float3(i.TBN0.w, i.TBN1.w, i.TBN2.w);
                fixed3 viewDir = normalize(UnityWorldSpaceViewDir(worldPos));
                fixed3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                fixed3 lightCol = _LightColor0.rgb;
                float2 speed = _Time.y * float2(_WaveXSpeed, _WaveYSpeed);

                // Calculate water boundaries (Same logic as in the Ground Pass)
                fixed puddleMask = tex2D(_PuddleMask, i.uv.zw);
                fixed groundHeight = tex2D(_HeightTex, i.uv.xy);
                float val = _HeightTexContrast / 2;
                fixed groundHeight_C = smoothstep(val, 1-val, groundHeight);
                puddleMask = max( 1-groundHeight_C, puddleMask );
                puddleMask = puddleMask > (1-_WetLevel) ? puddleMask : 0;

                float perceptualRoughness = 1 - _WaterSmoothness;
                float roughness = perceptualRoughness * perceptualRoughness;
                float squareRoughness = roughness * roughness;

                // Calculate water surface normal (ripples/waves) in tangent space
                fixed3 normal1_T = UnpackNormal(tex2D(_WaveMap, i.uv.xy + speed)).rgb;
                fixed3 normal2_T = UnpackNormal(tex2D(_WaveMap, i.uv.xy - speed)).rgb;
                fixed3 normal_T = normalize(normal1_T + normal2_T);
                normal_T.xy *= _WaveMapScale;
                normal1_T.z = 1 - sqrt(saturate(dot(normal_T.xy, normal_T.xy)));

                // Add raindrop ripples to the water normal
                fixed3 rippleNormal = ComputeRipple(_RippleTex ,i.uv_ripple, _Time.z);
                normal_T.xy = normal_T.xy + rippleNormal.xy * _RippleIntens * puddleMask;

                // Convert water normal from Tangent Space to World Space
                float3 worldNormal = normalize(float3(
                dot(i.TBN0.xyz, normal_T), 
                dot(i.TBN1.xyz, normal_T), 
                dot(i.TBN2.xyz, normal_T)) );

                // Calculate angles
                float3 h = normalize(viewDir + lightDir);
                float nl = max(saturate(dot(worldNormal, lightDir)), 0.00001);
                float nh = max(saturate(dot(worldNormal, h)), 0.00001);
                float nv = max(saturate(dot(worldNormal, viewDir)), 0.00001);
                float hv = max(saturate(dot(h, viewDir)), 0.00001);
                               
    // DIRECT LIGHT (WATER SURFACE) ---------------------------------------------------------------------------
                
                // Direct Light - Specular
                    // NDF 
                    float lerpSquareRoughness = pow( lerp( 0.002, 1.0, roughness ), 2 );
                    float D = lerpSquareRoughness
                    / ( UNITY_PI * pow(( nh*nh ) * ( lerpSquareRoughness - 1 ) + 1, 2) );
                    // Geometry Function
                    float k_DL = exp2(roughness + 1) / 8;
                    float G_in = nl / lerp( nv, 1.0, k_DL );
                    float G_out = nv / lerp( nv, 1.0, k_DL);
                    float G = G_in * G_out;
                    // Fresnel (Water reflectivity changes based on viewing angle)
                    float3 F0 = 0.02; // Standard F0 value for water is usually around 0.02
                    float3 F = F0 + (1-F0) * exp2((-5.55473*nv - 6.98316)*nv);
                    // PBR BRDF
                    float BRDF_DL_spec = D * F * G * 0.25 / ( nv*nl );
                    fixed3 specCol_DL = lightCol * BRDF_DL_spec * nl * UNITY_PI;
                    // specCol_DL = lightCol * pow( nh, _Gloss ); // Older generation (Blinn-phong) method commented out

                // Direct Light - Diffuse
                    fixed fresnel = F.r;    // We will assign the fresnel effect to the Alpha channel (transparency)
                    // fixed3 diffCol_DL =  lightCol * _WaterTint * nl ; // Water typically doesn't diffusely reflect light on its own, so it's disabled

                // Direct Light Result on Water Surface
                fixed3 col_DL = specCol_DL;

    // INDIRECT LIGHT (ENVIRONMENTAL REFLECTION - WATER SURFACE) ---------------------------------------------------------------------------
                // Taking 100% specular (mirror-like) environmental reflection for the water surface
                float3 reflVec = reflect(-viewDir, worldNormal);
                half4 rgbm = UNITY_SAMPLE_TEXCUBE(unity_SpecCube0, reflVec);
                float3 col_IDL = DecodeHDR(rgbm, unity_SpecCube0_HDR);

                // Final color of the water (Direct sun/light reflection + Sky reflection)
                fixed3 finalCol = col_DL + col_IDL;
                
                fixed4 col = 1;
                // Visibility (Alpha) of the water is determined by Fresnel and the Puddle mask.
                // This means looking straight down shows the ground below, looking parallel reflects the sky.
                col.a = puddleMask * fresnel ; 
                col.rgb = finalCol;
                return col;
            }
            ENDCG
        }
    }
}