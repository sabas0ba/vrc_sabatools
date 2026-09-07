Shader "Hidden/SabaTools/AvatarMaterialStudio/Invisible"
{
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
        Pass
        {
            ZWrite Off
            ColorMask 0
        }
    }
}
