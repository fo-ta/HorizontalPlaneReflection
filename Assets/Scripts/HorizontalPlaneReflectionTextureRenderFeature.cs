using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class HorizontalPlaneReflectionTextureRenderFeature : ScriptableRendererFeature
{
    /// <summary> RenterTargetHandle </summary>
    private static RTHandle _RTHandle;

    /// <summary> RenderTexture 名 </summary>
    private const string RTName = "_HorizontalPlaneReflectionTexture";

    /// <summary> RenderTexture ID </summary>
    private static readonly int RTNameID = Shader.PropertyToID(RTName);

    /// <summary> RenderTargetIdentifier </summary>
    private static readonly RenderTargetIdentifier RTIdentifier = new RenderTargetIdentifier(RTNameID);

    /// <summary> Opaque Pass </summary>
    private HorizontalPlaneReflectionTextureRenderOpaquePass _renderOpaquePass;

    /// <summary> Transparent Pass </summary>
    private HorizontalPlaneReflectionTextureRenderTransparentPass _renderTransparentPass;

    private HorizontalPlaneReflectionDebugPass _debugPass;

    /// <summary> Y座標を Plane 設定しに応じて平行移動する行列 </summary>
    private static Matrix4x4 _translateMat;

    /// <summary> Y軸反転する行列 </summary>
    private static Matrix4x4 _reverseMat;

    /// <summary> RenderTexture の ClearColor </summary>
    private static readonly Color ClearColor = new Color(0,0,0,0);

    /// <inheritdoc cref="Settings"/>
    [SerializeField] public Settings settings = new Settings();

    /// <summary> 各種設定クラス </summary>
    [Serializable]
    public class Settings
    {
        /// <summary> レイヤーカリングマスク </summary>
        public LayerMask cullingMask = -1;

        /// <summary> 反転平面の高さ </summary>
        public float planeHeight = 0;

        /// <summary> 反転平面の厚さ </summary>
        [Min(0)] public float planeThickness = 0;

        /// <summary> 鏡像のフェードを開始する高さ </summary>
        [Min(0)] public float fadeBaseHeight = 0;

        /// <summary> 鏡像のフェードの長さ </summary>
        [Min(0)] public float fadeRange = 1;

        /// <summary> 不透明鏡像パス有効化 </summary>
        public bool drawOpaque = true;

        /// <summary> 透明鏡像パス有効化 </summary>
        public bool drawTransparent = false;

        /// <summary> RenderPassEvent </summary>
        public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;

        /// <summary> DownScale 値 </summary>
        [Range(0.01f, 2)] public float renderTextureScale = 1.0f;

        /// <summary> FilterMode </summary>
        public FilterMode filterMode = FilterMode.Point;

        /// <summary> LDR レンダーテクスチャーフォーマット </summary>
        public RenderTextureFormat renderTextureFormat = RenderTextureFormat.Default;

        public bool renderTextureDebugView;
    }

    /// <summary>
    /// Reflection Pass 基底クラス
    /// </summary>
    private abstract class HorizontalPlaneReflectionTextureRenderPassBase : ScriptableRenderPass
    {

        // ShaderPropertyIDs
        private static readonly int FadeBaseHeightId = Shader.PropertyToID("_ReflectionFadeBaseHeight");
        private static readonly int FadeRangeId = Shader.PropertyToID("_ReflectionFadeRange");
        private static readonly int PlaneHeightId = Shader.PropertyToID("_ReflectionPlaneHeight");

        /// <summary> 初期化を行うフラグ </summary>
        public bool DoInitialize;

        /// <summary> クリーンアップを行うフラグ </summary>
        public bool DoCleanup;

        /// <summary> Settings Instance </summary>
        protected Settings Settings;

        /// <summary> Filter Settings </summary>
        protected FilteringSettings FilteringSettings;

        /// <inheritdoc cref="ScriptableRenderPass.OnCameraSetup"/>
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            if (!DoInitialize)
            {
                return;
            }

            _RTHandle = RTHandles.Alloc(RTIdentifier);
        }

        /// <inheritdoc cref="ScriptableRenderPass.Configure"/>
        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            ConfigureTarget(_RTHandle);

            if (!DoInitialize)
            {
                return;
            }

            cameraTextureDescriptor.colorFormat = Settings.renderTextureFormat;
            cameraTextureDescriptor.width = (int)(cameraTextureDescriptor.width * Settings.renderTextureScale);
            cameraTextureDescriptor.height = (int)(cameraTextureDescriptor.height * Settings.renderTextureScale);

            cmd.GetTemporaryRT(RTNameID, cameraTextureDescriptor, Settings.filterMode);

            ConfigureClear(ClearFlag.All, ClearColor);
        }

        /// <inheritdoc cref="ScriptableRenderPass.OnCameraCleanup"/>
        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            if (!DoCleanup)
            {
                return;
            }

            cmd.ReleaseTemporaryRT(RTNameID);
            RTHandles.Release(_RTHandle);
        }

        /// <summary>
        /// 反転用 ShaderGlobalParameter を設定
        /// </summary>
        /// <param name="cmd">CommandBuffer</param>
        protected void SetGlobalParameters(CommandBuffer cmd)
        {
            cmd.SetGlobalFloat(FadeRangeId, Settings.fadeRange);
            cmd.SetGlobalFloat(FadeBaseHeightId, Settings.fadeBaseHeight);
            // 高さぴったりにすると床にピッタリ着く面も消してしまうため、少し余裕を持たせる
            cmd.SetGlobalFloat(PlaneHeightId, Settings.planeHeight - 0.01f);
        }

        /// <summary>
        /// 反転用 Matrix と Culling を設定
        /// </summary>
        /// <param name="cmd">CommandBuffer</param>
        /// <param name="viewMatrix">ViewMatrix</param>
        /// <param name="projectionMatrix">ProjectionMatrix</param>
        protected static void SetReflect(CommandBuffer cmd, Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix)
        {
            // 反転用に View 行列を加工する
            // 変換後の頂点座標 = P * V * Reverse * Translate * M * 頂点座標
            viewMatrix = viewMatrix * _reverseMat * _translateMat; 
            RenderingUtils.SetViewAndProjectionMatrices(cmd, viewMatrix, projectionMatrix, false);

            // カリング反転 (ビュー行列を反転すると、メッシュの表・裏が逆転するため)
            cmd.SetInvertCulling(true);
        }

        /// <summary>
        /// 反転用 Matrix と Culling を設定を元に戻す
        /// </summary>
        /// <param name="cmd">CommandBuffer</param>
        /// <param name="context">ScriptableRenderContext</param>
        /// <param name="viewMatrix">元に戻す用の ViewMatrix</param>
        /// <param name="projectionMatrix">ProjectionMatrix</param>
        protected static void ResetReflect(CommandBuffer cmd, ScriptableRenderContext context, Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix)
        {
            // カリング反転を戻す
            cmd.SetInvertCulling(false);
            // View 行列を戻す
            RenderingUtils.SetViewAndProjectionMatrices(cmd, viewMatrix, projectionMatrix, false);
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
        }
    }

    /// <summary>
    /// Opaque Reflection Pass
    /// </summary>
    private class HorizontalPlaneReflectionTextureRenderOpaquePass : HorizontalPlaneReflectionTextureRenderPassBase
    {
        /// <summary> パス名 </summary>
        private const string PassName = nameof(HorizontalPlaneReflectionTextureRenderOpaquePass);

        /// <summary> レンダリング対象の ShaderTagID </summary>
        private readonly ShaderTagId _opaqueShaderTagId = new ShaderTagId("HorizontalPlaneReflectionOpaque");

        /// <summary> レンダリング対象の Depth Pass の ShaderTagID </summary>
        private readonly ShaderTagId _depthShaderTagId = new ShaderTagId("DepthOnly");

        /// <inheritdoc cref="ScriptableRenderPass.Execute"/>
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var sortingCriteria = renderingData.cameraData.defaultOpaqueSortFlags;
            var drawingSettings = CreateDrawingSettings(
                _opaqueShaderTagId,
                ref renderingData,
                sortingCriteria);
            var depthDrawingSettings = CreateDrawingSettings(
                _depthShaderTagId,
                ref renderingData,
                sortingCriteria);

            var cullingResults = renderingData.cullResults;

            var cmd = CommandBufferPool.Get(PassName);
            
            var cameraData = renderingData.cameraData;
            var defaultViewMatrix = cameraData.GetViewMatrix();
            var projectionMatrix = cameraData.GetProjectionMatrix();
            projectionMatrix = GL.GetGPUProjectionMatrix(projectionMatrix, cameraData.IsCameraProjectionMatrixFlipped());

            // 反転設定
            SetReflect(cmd, defaultViewMatrix, projectionMatrix);
            SetGlobalParameters(cmd);
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            // Draw Depth
            context.DrawRenderers(cullingResults, ref depthDrawingSettings, ref FilteringSettings);
            // Draw Color
            context.DrawRenderers(cullingResults, ref drawingSettings, ref FilteringSettings);

            // 反転を元に戻す
            ResetReflect(cmd, context, defaultViewMatrix, projectionMatrix);

            CommandBufferPool.Release(cmd);
        }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="settings">Settings</param>
        public HorizontalPlaneReflectionTextureRenderOpaquePass(Settings settings)
        {
            this.Settings = settings;
            renderPassEvent = settings.renderPassEvent;

            // Set FilteringSettings
            FilteringSettings = new FilteringSettings(RenderQueueRange.opaque, settings.cullingMask);
        }
    }

    /// <summary>
    /// Transparent Reflection Pass
    /// </summary>
    private class HorizontalPlaneReflectionTextureRenderTransparentPass : HorizontalPlaneReflectionTextureRenderPassBase
    {
        /// <summary> パス名 </summary>
        private const string PassName = nameof(HorizontalPlaneReflectionTextureRenderTransparentPass);

        // レンダリング対象のShaderTag
        private readonly List<ShaderTagId> _opaqueShaderTagIdList = new List<ShaderTagId>
        {
            new ShaderTagId("HorizontalPlaneReflectionTransparent"),
        };

        /// <inheritdoc cref="ScriptableRenderPass.Execute"/>
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var sortingCriteria = SortingCriteria.CommonTransparent;
            var drawingSettings = CreateDrawingSettings(
                _opaqueShaderTagIdList,
                ref renderingData,
                sortingCriteria);

            var cullingResults = renderingData.cullResults;

            var cmd = CommandBufferPool.Get(PassName);

            var cameraData = renderingData.cameraData;
            var defaultViewMatrix = cameraData.GetViewMatrix();
            var projectionMatrix = cameraData.GetProjectionMatrix();
            projectionMatrix = GL.GetGPUProjectionMatrix(projectionMatrix, cameraData.IsCameraProjectionMatrixFlipped());

            // 反転設定
            SetReflect(cmd, defaultViewMatrix, projectionMatrix);
            SetGlobalParameters(cmd);
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            // Draw
            context.DrawRenderers(cullingResults, ref drawingSettings, ref FilteringSettings);

            // 反転を元に戻す
            ResetReflect(cmd, context, defaultViewMatrix, projectionMatrix);

            CommandBufferPool.Release(cmd);
        }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="settings">Settings</param>
        public HorizontalPlaneReflectionTextureRenderTransparentPass(Settings settings)
        {
            this.Settings = settings;
            renderPassEvent = settings.renderPassEvent;

            // Set FilteringSettings
            FilteringSettings = new FilteringSettings(RenderQueueRange.transparent, settings.cullingMask);
        }
    }

    /// <summary>
    /// RenderTexture デバッグ表示表パス
    /// </summary>
    private class HorizontalPlaneReflectionDebugPass : ScriptableRenderPass
    {
        /// <summary> パス名 </summary>
        private const string PassName = nameof(HorizontalPlaneReflectionTextureRenderTransparentPass);

        /// <inheritdoc cref="ScriptableRenderPass.Execute"/>
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_RTHandle == null)
            {
                return;
            }

            var cmd = CommandBufferPool.Get(PassName);
            cmd.Blit(
                RTIdentifier,
                renderingData.cameraData.renderer.cameraColorTarget);
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public HorizontalPlaneReflectionDebugPass()
        {
            renderPassEvent = RenderPassEvent.AfterRendering;
        }
    }

    /// <inheritdoc cref="ScriptableRendererFeature.Create"/>
    public override void Create()
    {
        if (!settings.drawOpaque && !settings.drawTransparent)
        {
            return;
        }

        // Y座標を反転平面の高さと厚み分平行移動する行列
        _translateMat = Matrix4x4.identity;
        _translateMat.m13 = settings.planeThickness - settings.planeHeight;

        // Y軸反転する行列
        _reverseMat = Matrix4x4.identity;
        _reverseMat.m11 = -_reverseMat.m11;

        switch (settings.drawOpaque, settings.drawTransparent)
        {
            case (true, true):
                // Opaque, Transparent 両方
                _renderOpaquePass = new HorizontalPlaneReflectionTextureRenderOpaquePass(settings);
                _renderTransparentPass = new HorizontalPlaneReflectionTextureRenderTransparentPass(settings);
                _renderOpaquePass.DoInitialize = true;
                _renderTransparentPass.DoCleanup = true;
                break;
            case (true, false):
                // Opaque
                _renderOpaquePass = new HorizontalPlaneReflectionTextureRenderOpaquePass(settings);
                _renderOpaquePass.DoInitialize = true;
                _renderOpaquePass.DoCleanup = true;
                break;
            case (false, true):
                // Transparent
                _renderTransparentPass = new HorizontalPlaneReflectionTextureRenderTransparentPass(settings);
                _renderTransparentPass.DoInitialize = true;
                _renderTransparentPass.DoCleanup = true;
                break;
        }

        // UniversalRenderData の Compatibility/IntermediateTexture が Auto の際に正しく反映されない対策
        _renderOpaquePass?.ConfigureInput(ScriptableRenderPassInput.None);
        _renderTransparentPass?.ConfigureInput(ScriptableRenderPassInput.None);

        if (settings.renderTextureDebugView)
        {
            _debugPass = new HorizontalPlaneReflectionDebugPass();
        }
    }

    /// <inheritdoc cref="ScriptableRendererFeature.AddRenderPasses"/>
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.drawOpaque)
        {
            renderer.EnqueuePass(_renderOpaquePass);
        }

        if (settings.drawTransparent)
        {
            renderer.EnqueuePass(_renderTransparentPass);
        }

        if (settings.renderTextureDebugView)
        {
            renderer.EnqueuePass(_debugPass);
        }
    }
}
