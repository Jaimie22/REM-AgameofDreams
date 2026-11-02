
//  VOID SHADER (URP)
//  Creates a dark, starry "void" surface with a feeling of depth,
//  inspired by the End portal look.


Shader "Void"
{

    Properties
    {
        // The background colour of the void (keep it very dark)
        _BaseColor ("Void Colour", Color) = (0.01, 0.01, 0.03, 1)

        // Stars get a random colour somewhere between A and B.
        // [HDR] lets the colour go brighter than normal, which makes
        // it glow when you have Bloom turned on.
        [HDR] _ColorA ("Star Colour A", Color) = (0.3, 0.9, 0.8, 1)
        [HDR] _ColorB ("Star Colour B", Color) = (0.6, 0.3, 1.0, 1)

        // Scales the whole pattern up or down across the surface
        _Tiling ("Tiling", Float) = 1

        // How many star layers to stack (more layers = more depth,
        // but slightly more work for the graphics card)
        [IntRange] _LayerCount ("Layer Count", Range(1, 8)) = 6

        // How far apart each layer is "behind" the surface.
        // Higher = stronger parallax = deeper looking void.
        _LayerDepth ("Layer Depth", Range(0, 1)) = 0.15

        // How small the grid is that stars are placed in.
        // Higher = more, smaller star spots.
        _StarDensity ("Star Density", Range(1, 50)) = 12

        // The chance that any grid cell actually has a star in it.
        // 0 = no stars, 1 = a star in every cell.
        _StarChance ("Star Chance", Range(0, 1)) = 0.35

        // How big each star is
        _StarSize ("Star Size", Range(0.01, 0.5)) = 0.08

        // Overall brightness of all the stars
        _Brightness ("Brightness", Range(0, 5)) = 1.5

        // How much dimmer each deeper layer gets.
        // Makes far-away layers feel further away.
        _DepthFade ("Depth Fade", Range(0, 1)) = 0.25

        // How fast the layers slowly drift (negative = other way)
        _ScrollSpeed ("Scroll Speed", Range(-0.5, 0.5)) = 0.02

        // How fast the stars twinkle (0 = no twinkle)
        _TwinkleSpeed ("Twinkle Speed", Range(0, 10)) = 2
    }

    SubShader
    {
        // Tells Unity this is a solid (non-transparent) URP shader
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "Void"

            // The standard URP pass for drawing objects to the screen
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM

            // Tell Unity which functions do the work:
            // "vert" runs once per corner (vertex) of the mesh,
            // "frag" runs once per pixel on screen.
            #pragma vertex vert
            #pragma fragment frag

            // URP's built-in helper functions (camera position etc.)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            
            //  The code's copies of the Inspector properties.
            //  Names must match the Properties block exactly.
            //  Wrapping them in CBUFFER keeps URP's SRP Batcher happy
            //  (which helps performance).
            
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _ColorA;
                half4 _ColorB;
                float _Tiling;
                float _LayerCount;
                float _LayerDepth;
                float _StarDensity;
                float _StarChance;
                float _StarSize;
                float _Brightness;
                float _DepthFade;
                float _ScrollSpeed;
                float _TwinkleSpeed;
            CBUFFER_END

           
            //  ATTRIBUTES: the data we read from the mesh itself        
            struct Attributes
            {
                float4 positionOS : POSITION;   
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;    
                float2 uv         : TEXCOORD0;  
            };

            
            //  VARYINGS: data we pass from vert to frag.
            //  Unity blends these smoothly across the surface.
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION; 
                float2 uv         : TEXCOORD0;   
                float3 viewDirTS  : TEXCOORD1;   
                                                 
            };

            
            //  Hash21: turns a 2D position into a "random" number 0-1.
            //  It's not truly random - the same input always gives the
            //  same output - which is exactly what we want, so stars
            //  stay in place instead of flickering every frame.
            
            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            
            //  Hash22: same idea, but gives back TWO random numbers.
            //  used it to pick a random spot for a star in its cell.
            
            float2 Hash22(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.xx + p3.yz) * p3.zy);
            }

            //  Rotate: spins a set of UV coordinates by an angle
            //  (in radians).
            float2 Rotate(float2 uv, float angle)
            {
                float s = sin(angle);
                float c = cos(angle);
                return float2(uv.x * c - uv.y * s, uv.x * s + uv.y * c);
            }

           
            //  StarLayer: draws ONE layer of stars.
            float3 StarLayer(float2 uv, float seed)
            {
                // Scale up the UVs so we get lots of grid cells
                uv *= _StarDensity;

                // cell = which grid square we're in (whole numbers)
                // f    = where we are INSIDE that square (0 to 1)
                float2 cell = floor(uv);
                float2 f = frac(uv);

                // Start with no light, and add stars to it
                float3 col = 0;

                // Check our own cell AND the 8 cells around it.
                // Without this, a star near the edge of a cell would
                // get chopped off where the next cell begins.
                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        // Which neighbouring cell we're checking
                        float2 offset = float2(x, y);

                        // A unique ID for that cell (plus the layer seed)
                        float2 id = cell + offset + seed * 17.0;

                        // Roll the dice: does this cell have a star?
                        float chance = Hash21(id);
                        if (chance > _StarChance) continue; // no star, skip

                        // Random position of the star inside its cell
                        float2 starPos = Hash22(id);

                        // How far this pixel is from the star's centre
                        float dist = length(f - offset - starPos);

                        // Give each star a slightly different size (50%-150%)
                        float sizeRand = lerp(0.5, 1.5, Hash21(id + 7.1));

                        // Bright at the centre, fading to nothing at the edge
                        float star = smoothstep(_StarSize * sizeRand, 0.0, dist);

                        // Gentle pulsing brightness. Each star gets its
                        // own timing so they don't all blink together.
                        float twinkle = 0.6 + 0.4 * sin(_Time.y * _TwinkleSpeed + chance * 100.0);

                        // Pick a colour somewhere between Colour A and B
                        float3 tint = lerp(_ColorA.rgb, _ColorB.rgb, Hash21(id + 3.7));

                        // Add this star's light to the total
                        col += star * twinkle * tint;
                    }
                }
                return col;
            }

           
            //  VERTEX FUNCTION
            //  Runs once for each corner of the mesh.
            
            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                // Let URP work out the vertex's world and screen positions
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);

                // Let URP work out the surface directions in world space
                // (normal = out of the surface, tangent/bitangent = along it)
                VertexNormalInputs nrm = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

                // Where the vertex appears on screen
                OUT.positionCS = pos.positionCS;

                // Apply tiling to the UVs
                OUT.uv = IN.uv * _Tiling;

                // Direction from this point on the surface to the camera
                float3 viewWS = GetCameraPositionWS() - pos.positionWS;

                // Convert that direction into the surface's own space:
                OUT.viewDirTS = float3(
                    dot(viewWS, nrm.tangentWS),
                    dot(viewWS, nrm.bitangentWS),
                    dot(viewWS, nrm.normalWS));

                return OUT;
            }

            
            //  FRAGMENT FUNCTION
            //  Runs once for every pixel of the surface on screen,
            //  and decides what colour that pixel should be.
            
            half4 frag(Varyings IN) : SV_Target
            {
                // Make the camera direction a length of 1
                float3 v = normalize(IN.viewDirTS);

                // How much to slide the layers based on viewing angle.
                float2 parallaxDir = v.xy / max(v.z, 0.15);

                // Start with the dark void colour
                float3 col = _BaseColor.rgb;

                // How many layers to draw (from the Inspector)
                int count = (int)_LayerCount;

                // Draw each star layer, one after another.
                [loop]
                for (int i = 0; i < 8; i++)
                {
                    // Stop once we've drawn the number of layers we want
                    if (i >= count) break;

                    // Each layer sits further back than the last
                    float depth = (i + 1) * _LayerDepth;

                    // THE PARALLAX TRICK:
                    // Slide this layer's UVs based on the camera angle.
                    // Deeper layers slide further, creating the depth.
                    float2 uv = IN.uv - parallaxDir * depth;

                    // Turn each layer to a different angle so the
                    // layers don't line up and look repetitive
                    uv = Rotate(uv, i * 1.3);

                    // Slowly drift each layer over time.
                    // Deeper layers drift slightly faster, so the
                    // layers move against each other.
                    uv += float2(0.0, _Time.y * _ScrollSpeed * (1.0 + i * 0.35));

                    // Make deeper layers dimmer, so they feel further away
                    float fade = exp(-i * _DepthFade);

                    // Draw this layer's stars and add them to the colour
                    col += StarLayer(uv, i) * fade * _Brightness;
                }

                // Output the final colour (fully solid)
                return half4(col, 1);
            }

            ENDHLSL
        }
    }
}