Shader "UI/ProgressBar"
{
    Properties
    {
        _FillTexA ("Fill Texture A", 2D) = "white" {}
        _FillTexB ("Fill Texture B", 2D) = "white" {}
        _EmptyTex ("Empty Texture", 2D) = "white" {}

        _Progress ("Progress", Range(0,1)) = 0.5

        [Enum(Horizontal,0,Vertical,1)]
        _Axis ("Axis", Float) = 0

        [Enum(Forward,0,Reverse,1)]
        _Direction ("Direction", Float) = 0

        _Start ("Start", Range(0,1)) = 0
        _End ("End", Range(0,1)) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "CanUseSpriteAtlas"="True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]

        Pass
        {
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            sampler2D _FillTexA;
            sampler2D _FillTexB;
            sampler2D _EmptyTex;

            float _Progress;
            float _Axis;
            float _Direction;
            float _Start;
            float _End;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 fillA = tex2D(_FillTexA, i.uv);
                fixed4 fillB = tex2D(_FillTexB, i.uv);
                fixed4 empty = tex2D(_EmptyTex, i.uv);

                // Horizontal or vertical position
                float position = lerp(i.uv.x, i.uv.y, _Axis);

                // Reverse direction
                position = lerp(position, 1.0 - position, _Direction);

                // Remapped progress range
                float cutoff = lerp(_Start, _End, _Progress);

                // Fill / empty mask
                float mask = step(position, cutoff);

                // Lerp between the two fill textures based on progress
                fixed4 fill = lerp(fillA, fillB, _Progress);

                // Fill or empty
                fixed4 result = lerp(empty, fill, mask);

                // UI color / alpha
                result *= i.color;

                return result;
            }

            ENDHLSL
        }
    }
}