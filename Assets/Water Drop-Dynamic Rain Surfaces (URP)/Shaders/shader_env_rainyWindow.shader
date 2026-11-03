Shader "Water Drop-Dynamic Rain Surfaces (URP)/RainyWindow"
{
    Properties
    {
        // The background texture (Usually left empty because the shader grabs the screen behind it)
        _MainTex ("Texture", 2D) = "" {}
        
        // Controls the size/density of the raindrop grid
        _Size ("Grid_Size", Range(1,10) ) = 1
        
        // How much the water droplets distort the image behind them
        _Distortion("Distortion", Range(0, 10)) = 1
        
        // How blurry the "fogged" or wet glass is
        _Blur ("Blur", Range(0,1)) = 0.3
    }
    SubShader
    {
        // Render after opaque objects so we can see the scene behind the window
        Tags { "Queue"="Transparent" "RenderPipeline"="UniversalPipeline"}
        LOD 100
        Cull Off // Renders both sides of the glass
        ZWrite Off // Doesn't write to the depth buffer so it doesn't block other transparents
        Blend SrcAlpha OneMinusSrcAlpha // Standard transparency blending

        Pass
        {
            Tags {"LightMode"="UniversalForward"}

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag


            
            // A macro to replace the word 'S' with the built-in 'smoothstep' function for shorter code
            // smoothstep(a, b, t) creates a smooth transition between 'a' and 'b' based on 't'
            #define S(a,b,t) smoothstep(a,b,t)

            #include "UnityCG.cginc"

            // Input data from the 3D model
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            // Data passed from the Vertex Shader to the Fragment Shader
            struct v2f
            {
                float2 uv : TEXCOORD0;
                
                // Coordinates used to sample the screen behind the object (for distortion/glass effect)
                float4 grabUV : TEXCOORD1;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Size, _Distortion, _Blur;

            // VERTEX SHADER
            v2f vert (appdata v)
            {
                v2f o;
                // Convert 3D local position to 2D screen position
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                
                // Calculates the exact screen coordinates where this object is currently being rendered
                // This is crucial for creating glass/refraction effects
                o.grabUV = ComputeGrabScreenPos(o.vertex);
                
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            // Pseudo-random number generator that takes a 2D coordinate and returns a value from 0 to 1
            float N21(float2 p) {
                p = frac(p*float2(123.34, 345.34));
                p += dot(p, p+34.345);

                return frac(p.x*p.y);
                }

            // CORE PROCEDURAL RAINDROP GENERATION FUNCTION
            // This function creates a grid, calculates falling droplets, and their trails
            float3 Layer(float2 UV, float t) {

                // grid aspect ratio (makes the grid rectangular so drops can fall downwards more naturally)
                float2 aspect = float2(2,1);
                float2 uv = UV;

                // Create a repeating grid for the droplets based on the _Size property
                float2 uv_drop = (UV * _Size * aspect);
                uv_drop.y += t * 0.5; // Moves the grid downwards over time (simulating gravity)
                
                // Get a unique whole-number ID for each grid cell to generate random values per cell
                float2 id = floor(uv_drop);
                
                // Generate a random number for this specific cell
                float n = N21(id);
                t += n*6.28; // Randomize the time offset so drops don't fall in sync
                
                // Get local coordinates inside the grid cell (-0.5 to 0.5)
                uv_drop = frac(uv_drop) - 0.5;
                
                // Create a separate grid space for the trails behind the droplets
                float2 uv_trail = (UV * _Size * aspect);
                uv_trail.y *= 8; // Stretch the Y axis to make long trails
                uv_trail = frac(uv_trail) - 0.5;
                
                // Calculate the erratic, wavy falling path of the droplet using sine waves
                float w = UV.y * 10;
                float x = ( n - 0.5 ) * 0.8; // Random starting X position within the cell: (-0.4, 0.4)
                x += (0.4 - abs(x)) * sin(3*w) * pow(sin(w), 6) * 0.45; // Add side-to-side wiggle
                
                // Calculate the Y position (falling motion with varying speed)
                float y = -sin(t + sin(t+sin(t))) * 0.45;
                y -= pow(uv_drop.x - x, 2); // Shape the droplet (makes it slightly tear-shaped)

                // Define the center positions for the droplet and its trail
                float2 dropPos = (uv_drop - float2(x,y)) / aspect;
                float2 trailPos = (uv_trail - float2(x,y)) / aspect;
                trailPos.y = (trailPos.y - 0.1) / 8; // Offset the trail to be above the droplet

                // Draw the actual droplet shape using smoothstep
                float droplet = S(.05,.03,length(dropPos));
                
                // Draw the trail shape
                float trail = S(.03,.01,length(trailPos));
                
                // Remove the trail where the droplet currently is
                float fogTrail = S(-.03, .03, dropPos.y);
                
                // Fade out the trail gradually towards the top (gradient)
                fogTrail *= S(0.5, 0, uv_drop.y);
                trail *= fogTrail;

                // Taper the trail horizontally so it's not a blocky line
                fogTrail *= S(.05, .01, abs(dropPos.x));

                // Combine droplet and trail vectors into an offset map (used for distortion)
                float2 offset = droplet * dropPos + trail * trailPos;
                
                // Return float3: (X offset, Y offset, Trail Alpha mask)
                // The 'fogTrail' mask tells the shader where the glass should be "wiped clean"
                return float3(offset, fogTrail);
                }

            // FRAGMENT SHADER
            fixed4 frag (v2f i) : SV_Target
            {
                // Reset time every 7200 seconds (2 hours) to prevent floating-point precision glitches over long play sessions
                float t = fmod(_Time.y * 0.9, 7200);

                float4 col;
                
                // Generate 3 layers of droplets with different sizes and offsets to create depth/parallax
                float3 droplets = Layer(i.uv, t);
                droplets += Layer(i.uv * 1.2 + 3.5, t); // Scaled up and shifted
                droplets += Layer(i.uv * 1.6 - 1.5, t); // Scaled up even more and shifted

                // Calculate the blur amount. (1 - droplets.z) ensures that where the droplets and trails are, the blur is 0 (clear glass)
                float blur = _Blur /100 * (1-droplets.z);

                // Convert homogenous screen space coordinates to standard UV coordinates
                float2 projUV = i.grabUV.xy / i.grabUV.w;
                
                // Apply the droplet normal map (offset) to distort the screen behind the glass
                projUV += droplets.xy * _Distortion;

                // CUSTOM BLUR LOOP (Gaussian-style blur)
                const int numSamples = 20;
                float a = N21(i.uv)*6.28; // Randomize rotation angle per pixel to hide banding artifacts
                
                for (int i = 0; i < numSamples; i++) {

                    // Calculate circular offsets for sampling the texture
                    float2 offs = float2(sin(a), cos(a)) * blur;
                    
                    // Introduce pseudo-randomness to the offset distance to create a frosted glass effect
                    offs *= frac(sin(i+1.2)*13.5)*34.3;
                    
                    // Sample the screen behind the window with the offset and add to total color
                    col += tex2D(_MainTex, projUV+offs);
                    a++; // Rotate the sampling angle

                    }

                // Average the accumulated colors to finalize the blur
                col /= numSamples;

                return col;
            }
            ENDCG
        }
    }
}