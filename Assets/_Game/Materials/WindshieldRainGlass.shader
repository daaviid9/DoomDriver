Shader "DoomDriver/WindshieldRainGlass"
{
    Properties
    {
        _GlassTint ("Clean Glass Tint", Color) = (0.72, 0.84, 0.92, 0.08)
        _DropletColor ("Droplet Highlight Color", Color) = (0.90, 0.96, 1.0, 0.85)
        _RainIntensity ("Rain Intensity", Range(0, 1)) = 0.0
        _DropScale ("Droplet Scale", Float) = 11.0
        _DropSpeed ("Droplet Trickle Speed", Float) = 0.55
        _FlowTime ("Front Windshield Flow Time", Float) = 0.0
        _SideDownTime ("Side Window Downward Time", Float) = 0.0
        _SideBackTime ("Side Window Backward Wind Time", Float) = 0.0
        _SpeedFactor ("Normalized Vehicle Speed (0-1)", Range(0, 1)) = 0.0
        _StaticDropDensity ("Static Mist Density", Range(0, 1)) = 0.85

        [Header(Windshield Wipers)]
        _WiperActive ("Wiper Active (0-1)", Range(0, 1)) = 0.0
        _WiperAngle ("Wiper Current Angle (rad)", Float) = -1.38
        _WiperDir ("Wiper Sweep Direction (+1/-1)", Float) = 1.0
        _WiperMinAngle ("Wiper Park Angle (rad)", Float) = -1.38
        _WiperMaxAngle ("Wiper Max Sweep Angle (rad)", Float) = 1.05
        _WiperPivot1 ("Left Wiper Pivot (XY)", Vector) = (0.28, 0.88, 0, 0)
        _WiperPivot2 ("Right Wiper Pivot (XY)", Vector) = (-0.28, 0.88, 0, 0)
        _WiperLength ("Wiper Blade Length", Float) = 0.62
        _WiperColor ("Wiper Blade Color", Color) = (0.06, 0.06, 0.07, 0.96)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionOS  : TEXCOORD0;
                float3 normalOS    : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _GlassTint;
                float4 _DropletColor;
                float _RainIntensity;
                float _DropScale;
                float _DropSpeed;
                float _FlowTime;
                float _SideDownTime;
                float _SideBackTime;
                float _SpeedFactor;
                float _StaticDropDensity;

                float _WiperActive;
                float _WiperAngle;
                float _WiperDir;
                float _WiperMinAngle;
                float _WiperMaxAngle;
                float4 _WiperPivot1;
                float4 _WiperPivot2;
                float _WiperLength;
                float4 _WiperColor;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                // Pure object space so droplets & wipers stay rigidly glued to the glass
                // and tilt/roll identically with the car body during steering and bumps.
                OUT.positionOS = IN.positionOS.xyz;
                OUT.normalOS = normalize(IN.normalOS);
                return OUT;
            }

            // Fast 2D -> 1D hash
            float N21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            // Animated trickling raindrop layer moving along -uv.y as t increases
            float2 RainLayer(float2 uv, float t)
            {
                float2 aspect = float2(2.0, 1.0);
                float2 st = uv * aspect;

                st.y += t * 0.25;
                float2 id = floor(st);
                float n = N21(id);

                t += n * 6.2831;

                // Subtle lateral wiggle as drop moves
                float w = uv.y * 10.0;
                float x = (n - 0.5) * 0.8;
                x += (0.4 - abs(x)) * sin(3.0 * w) * pow(sin(w), 6.0) * 0.45;

                // Stick-slip motion along flow axis
                float y = -sin(t + sin(t + sin(t) * 0.5)) * 0.43;
                float2 gv = frac(st) - 0.5;
                y -= (gv.x - x) * (gv.x - x);

                // Leading droplet
                float2 dropPos = (gv - float2(x, y)) / aspect;
                float drop = smoothstep(0.055, 0.025, length(dropPos));

                // Trailing micro-beads behind the leading drop
                float2 trailPos = (gv - float2(x, t * 0.25)) / aspect;
                trailPos.y = (frac(trailPos.y * 8.0) - 0.5) / 8.0;
                float trail = smoothstep(0.032, 0.012, length(trailPos));
                float fogTrail = smoothstep(-0.05, 0.05, dropPos.y);
                fogTrail *= smoothstep(0.5, y, gv.y);
                trail *= fogTrail;
                fogTrail *= smoothstep(0.05, 0.04, abs(dropPos.x));

                float mask = max(drop, trail * 0.65);
                return float2(mask, fogTrail * 0.35);
            }

            // Fine static mist droplets that gently appear and fade on glass
            float StaticDrops(float2 uv, float t)
            {
                uv *= 3.2;
                float2 id = floor(uv);
                float2 gv = frac(uv) - 0.5;
                float n = N21(id);
                t += n * 6.2831;

                float2 offset = float2(frac(n * 34.5) - 0.5, frac(n * 71.3) - 0.5) * 0.6;
                float d = length(gv - offset);

                float fade = smoothstep(0.0, 0.25, frac(t * 0.12)) * smoothstep(1.0, 0.65, frac(t * 0.12));
                float radius = lerp(0.06, 0.15, frac(n * 17.1));
                return smoothstep(radius, radius * 0.4, d) * fade;
            }

            // Evaluates a single wiper blade + wiped-clean rain mask.
            // Returns float2(bladeAlpha, rainMultiplier) where rainMultiplier=0 means freshly wiped clean.
            float2 EvaluateWiper(float2 posXY, float2 pivot, float angle, float dir, float len)
            {
                float2 p = posXY - pivot;
                float r = length(p);

                // Blade unit direction (angle=0 is straight up +Y, +angle is left +X_os, -angle is right -X_os)
                float2 bladeDir = float2(sin(angle), cos(angle));
                float2 bladePerp = float2(bladeDir.y, -bladeDir.x);

                float along = dot(p, bladeDir);
                float perp = abs(dot(p, bladePerp));

                // 1. Visual wiper blade (rubber blade + thinner arm)
                float inBladeSpan = step(len * 0.20, along) * step(along, len);
                float bladeMask = (1.0 - smoothstep(0.008, 0.015, perp)) * inBladeSpan;

                float inArmSpan = step(0.0, along) * step(along, len * 0.60);
                float armMask = (1.0 - smoothstep(0.004, 0.009, perp)) * inArmSpan;

                float visualBlade = saturate(max(bladeMask, armMask));

                // 2. Wiped-clean rain arc strictly along the rubber blade path (len*0.20 .. len*1.00, minAngle .. maxAngle)
                float rainMult = 1.0;
                float arcRange = max(_WiperMaxAngle - _WiperMinAngle, 0.1);

                // Crisp radial bounds matching the exact rubber blade span
                float inArcRadius = smoothstep(len * 0.19, len * 0.21, r) * (1.0 - smoothstep(len * 0.99, len * 1.01, r));

                if (inArcRadius > 0.001 && _WiperActive > 0.01)
                {
                    float pixAngle = atan2(p.x, p.y);
                    // Crisp angular bounds matching the exact wiper sweep limits
                    float inSweep = smoothstep(_WiperMinAngle - 0.015, _WiperMinAngle + 0.015, pixAngle)
                                  * (1.0 - smoothstep(_WiperMaxAngle - 0.015, _WiperMaxAngle + 0.015, pixAngle));

                    if (inSweep > 0.001)
                    {
                        // Exact continuous angular distance traveled by the blade since it last wiped pixAngle
                        // (100% continuous at both turnaround points, zero popping on reversal)
                        float distSinceWipe;
                        if (dir >= 0.0)
                        {
                            distSinceWipe = (pixAngle <= angle)
                                ? (angle - pixAngle)
                                : ((angle - _WiperMinAngle) + (pixAngle - _WiperMinAngle));
                        }
                        else
                        {
                            distSinceWipe = (pixAngle >= angle)
                                ? (pixAngle - angle)
                                : ((_WiperMaxAngle - angle) + (_WiperMaxAngle - pixAngle));
                        }

                        // Normalize across a full round-trip (0 = just wiped clean, 1 = longest since wipe)
                        float normAge = saturate(distSinceWipe / (arcRange * 2.0));

                        // Completely clean (0.0) right behind the blade, smoothly reforming droplets over time
                        float clearedMult = smoothstep(0.06, 0.95, normAge) * 0.85;
                        rainMult = lerp(1.0, clearedMult, inArcRadius * inSweep * _WiperActive);
                    }
                }

                return float2(visualBlade, rainMult);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 absN = abs(IN.normalOS);
                bool isFrontGlass = (absN.z >= absN.x);

                // Evaluate wipers on front windshield only
                float wiperBladeAlpha = 0.0;
                float wiperRainMask = 1.0;

                if (isFrontGlass && _WiperLength > 0.01)
                {
                    float2 w1 = EvaluateWiper(IN.positionOS.xy, _WiperPivot1.xy, _WiperAngle, _WiperDir, _WiperLength);
                    float2 w2 = EvaluateWiper(IN.positionOS.xy, _WiperPivot2.xy, _WiperAngle, _WiperDir, _WiperLength);
                    wiperBladeAlpha = saturate(max(w1.x, w2.x)) * _WiperActive;
                    wiperRainMask = min(w1.y, w2.y);
                }

                float rain = saturate(_RainIntensity);
                half4 glassResult = half4(_GlassTint.rgb, _GlassTint.a);

                if (rain > 0.001)
                {
                    float staticT = _Time.y * max(_DropSpeed, 0.35);
                    float2 layer1;
                    float2 layer2;
                    float staticMask;

                    if (isFrontGlass)
                    {
                        float2 frontUV = float2(IN.positionOS.x, IN.positionOS.y) * _DropScale;
                        layer1 = RainLayer(frontUV, _FlowTime);
                        layer2 = RainLayer(frontUV * 1.65 + float2(7.3, 2.1), _FlowTime * 1.25);
                        staticMask = StaticDrops(frontUV, staticT) * _StaticDropDensity;
                    }
                    else
                    {
                        float windBlend = smoothstep(0.12, 0.65, _SpeedFactor);

                        float2 sideDownUV = float2(-IN.positionOS.z, IN.positionOS.y) * _DropScale;
                        float2 down1 = RainLayer(sideDownUV, _SideDownTime);
                        float2 down2 = RainLayer(sideDownUV * 1.65 + float2(5.1, 3.7), _SideDownTime * 1.2);

                        float2 sideBackUV = float2(IN.positionOS.y, -IN.positionOS.z) * _DropScale;
                        float2 back1 = RainLayer(sideBackUV, _SideBackTime);
                        float2 back2 = RainLayer(sideBackUV * 1.65 + float2(9.2, 1.4), _SideBackTime * 1.25);

                        layer1 = lerp(down1, back1, windBlend);
                        layer2 = lerp(down2, back2, windBlend);
                        staticMask = StaticDrops(sideDownUV, staticT) * _StaticDropDensity;
                    }

                    float dropHeight = saturate(max(max(layer1.x, layer2.x * 0.85), staticMask * 0.75)) * wiperRainMask;
                    float trailStreak = saturate(layer1.y + layer2.y * 0.7) * wiperRainMask;

                    float dx = ddx(dropHeight);
                    float dy = ddy(dropHeight);
                    float specHighlight = saturate((-dx + dy) * 3.5);
                    float darkRim = saturate((dx - dy) * 2.5);

                    float3 dropCol = _DropletColor.rgb * (0.72 + specHighlight * 0.85 - darkRim * 0.35);
                    float dropAlpha = saturate((dropHeight * 0.88 + trailStreak * 0.25) * rain * _DropletColor.a);

                    float finalAlpha = saturate(_GlassTint.a + dropAlpha);
                    float3 finalRgb = lerp(_GlassTint.rgb, dropCol, saturate(dropAlpha / max(finalAlpha, 0.001)));
                    glassResult = half4(finalRgb, finalAlpha);
                }

                // Composite wiper blades over the windshield glass
                if (wiperBladeAlpha > 0.001)
                {
                    float bladeA = wiperBladeAlpha * _WiperColor.a;
                    glassResult.rgb = lerp(glassResult.rgb, _WiperColor.rgb, bladeA);
                    glassResult.a = saturate(max(glassResult.a, bladeA));
                }

                return glassResult;
            }
            ENDHLSL
        }
    }
}
