
//----------------------------------------------------------------------------------------------------------
// X-PostProcessing Library
// created by QianMo @ 2020
//----------------------------------------------------------------------------------------------------------

using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;


namespace XPostProcessing
{

    [Serializable]
    [PostProcess(typeof(DualTentBlurRenderer), PostProcessEvent.AfterStack, "X-PostProcessing/Blur/DualTentBlur")]
    public class DualTentBlur : PostProcessEffectSettings
    {

        [Range(0.0f, 15.0f)]
        public FloatParameter BlurRadius = new FloatParameter { value = 5.0f };

        [Range(1.0f, 8.0f)]
        public IntParameter Iteration = new IntParameter { value = 4 };

        [Range(1, 10)]
        public FloatParameter RTDownScaling = new FloatParameter { value = 2 };
    }

    public sealed class DualTentBlurRenderer : PostProcessEffectRenderer<DualTentBlur>
    {

        private const string PROFILER_TAG = "X-DualTentBlur";
        private Shader shader;

        // [down,up]
        Level[] m_Pyramid;
        const int k_MaxPyramidSize = 16;

        public override void Init()
        {
            shader = Shader.Find("Hidden/X-PostProcessing/DualTentBlur");

            m_Pyramid = new Level[k_MaxPyramidSize];

            for (int i = 0; i < k_MaxPyramidSize; i++)
            {
                m_Pyramid[i] = new Level
                {
                    down = Shader.PropertyToID("_BlurMipDown" + i),
                    up = Shader.PropertyToID("_BlurMipUp" + i)
                };
            }
        }

        public override void Release()
        {
            base.Release();
        }

        static class ShaderIDs
        {
            internal static readonly int blurOffset = Shader.PropertyToID("_BlurOffset");
            internal static readonly int bufferRT1 = Shader.PropertyToID("_BufferRT1");
            internal static readonly int bufferRT2 = Shader.PropertyToID("_BufferRT2");
        }

        struct Level
        {
            internal int down;
            internal int up;
        }

        public override void Render(PostProcessRenderContext context)
        {

            CommandBuffer cmd = context.command;
            PropertySheet sheet = context.propertySheets.Get(shader);
            cmd.BeginSample(PROFILER_TAG);

            int tw = (int)(context.screenWidth / settings.RTDownScaling);
            int th = (int)(context.screenHeight / settings.RTDownScaling);

            Vector4 blurOffset = new Vector4(settings.BlurRadius / (float)context.screenWidth, settings.BlurRadius / (float)context.screenHeight, 0, 0);
            sheet.properties.SetVector(ShaderIDs.blurOffset, blurOffset);
            // Downsample
            RenderTargetIdentifier lastDown = context.source;
            for (int i = 0; i < settings.Iteration; i++)
            {
                int mipDown = m_Pyramid[i].down;
                int mipUp = m_Pyramid[i].up;
                context.GetScreenSpaceTemporaryRT(cmd, mipDown, 0, context.sourceFormat, RenderTextureReadWrite.Default, FilterMode.Bilinear, tw, th);
                context.GetScreenSpaceTemporaryRT(cmd, mipUp, 0, context.sourceFormat, RenderTextureReadWrite.Default, FilterMode.Bilinear, tw, th);
                cmd.BlitFullscreenTriangle(lastDown, mipDown, sheet, 0);

                lastDown = mipDown;
                tw = Mathf.Max(tw / 2, 1);
                th = Mathf.Max(th / 2, 1);
            }

            // Upsample
            int lastUp = m_Pyramid[settings.Iteration - 1].down;
            for (int i = settings.Iteration - 2; i >= 0; i--)
            {
                int mipUp = m_Pyramid[i].up;
                cmd.BlitFullscreenTriangle(lastUp, mipUp, sheet, 0);
                lastUp = mipUp;
            }


            // Render blurred texture in blend pass
            cmd.BlitFullscreenTriangle(lastUp, context.destination, sheet, 1);

            // Cleanup
            for (int i = 0; i < settings.Iteration; i++)
            {
                if (m_Pyramid[i].down != lastUp)
                    cmd.ReleaseTemporaryRT(m_Pyramid[i].down);
                if (m_Pyramid[i].up != lastUp)
                    cmd.ReleaseTemporaryRT(m_Pyramid[i].up);
            }

            cmd.EndSample(PROFILER_TAG);
        }
    }
}
        