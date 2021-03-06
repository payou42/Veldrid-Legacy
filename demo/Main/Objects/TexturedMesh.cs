﻿using ShaderGen;
using System.Numerics;
using System.Runtime.CompilerServices;
using Veldrid.Graphics;
using System;

namespace Veldrid.NeoDemo.Objects
{
    public class TexturedMesh : CullRenderable
    {
        private readonly MeshData _meshData;
        private readonly TextureData _textureData;
        private readonly Transform _transform = new Transform();

        private BoundingBox _centeredBounds;
        private VertexBuffer _vb;
        private IndexBuffer _ib;
        private int _indexCount;
        private ShaderSet _shaderSet;
        private ShaderResourceBindingSlots _resourceSlots;
        private DeviceTexture _texture;
        private ShaderTextureBinding _textureBinding;
        private DeviceTexture _alphamapTexture;
        private ShaderTextureBinding _alphamapBinding;

        private ShaderSet _mapSet;
        private ShaderResourceBindingSlots _mapSlots;

        private ConstantBuffer _worldBuffer;
        private ConstantBuffer _inverseTransposeWorldBuffer;

        private readonly MaterialPropsAndBuffer _materialProps;

        private bool _materialPropsOwned = false;

        public MaterialProperties MaterialProperties { get => _materialProps.Properties.Data; set { _materialProps.Properties.Data = value; } }

        public Transform Transform => _transform;

        public TexturedMesh(MeshData meshData, TextureData textureData, MaterialProperties materialProps)
            : this(meshData, textureData, new MaterialPropsAndBuffer(materialProps))
        {
            _materialPropsOwned = true;
        }

        public TexturedMesh(MeshData meshData, TextureData textureData, MaterialPropsAndBuffer materialProps)
        {
            _meshData = meshData;
            _centeredBounds = meshData.GetBoundingBox();
            _textureData = textureData;
            MaterialProperties defaultProps = new MaterialProperties { SpecularIntensity = new Vector3(0.3f), SpecularPower = 10f };
            _materialProps = materialProps;
        }

        public override BoundingBox BoundingBox => BoundingBox.Transform(_centeredBounds, _transform.GetTransformMatrix());

        public override void CreateDeviceObjects(RenderContext rc)
        {
            ResourceFactory factory = rc.ResourceFactory;
            _vb = _meshData.CreateVertexBuffer(factory);
            _ib = _meshData.CreateIndexBuffer(factory, out _indexCount);

            ShadowMainSetInfo.CreateAll(
                factory,
                ShaderHelper.LoadBytecode(factory, "ShadowMain", ShaderStages.Vertex),
                ShaderHelper.LoadBytecode(factory, "ShadowMain", ShaderStages.Fragment),
                out _shaderSet,
                out _resourceSlots);
            ShadowDepthSetInfo.CreateAll(
                factory,
                ShaderHelper.LoadBytecode(factory, "ShadowDepth", ShaderStages.Vertex),
                ShaderHelper.LoadBytecode(factory, "ShadowDepth", ShaderStages.Fragment),
                out _mapSet,
                out _mapSlots);

            _worldBuffer = factory.CreateConstantBuffer(ShaderConstantType.Matrix4x4);
            _inverseTransposeWorldBuffer = factory.CreateConstantBuffer(ShaderConstantType.Matrix4x4);
            if (_materialPropsOwned)
            {
                _materialProps.CreateDeviceObjects(rc);
            }

            _texture = _textureData.CreateDeviceTexture(factory);
            _textureBinding = factory.CreateShaderTextureBinding(_texture);
            _alphamapTexture = factory.CreateTexture(new RgbaByte[] { RgbaByte.White }, 1, 1, PixelFormat.R8_G8_B8_A8_UInt);
            _alphamapBinding = factory.CreateShaderTextureBinding(_alphamapTexture);
            CreateRasterizerState(rc);
        }

        public override void DestroyDeviceObjects()
        {
            _vb.Dispose();
            _ib.Dispose();
            _shaderSet.Dispose();
            _worldBuffer.Dispose();
            _inverseTransposeWorldBuffer.Dispose();
            _texture.Dispose();
            _textureBinding.Dispose();
            _rasterizerState?.Dispose();
            if (_materialPropsOwned)
            {
                _materialProps.DestroyDeviceObjects();
            }
            _alphamapTexture.Dispose();
            _alphamapBinding.Dispose();
        }

        public override RenderOrderKey GetRenderOrderKey(Vector3 cameraPosition)
        {
            return RenderOrderKey.Create(_shaderSet.GetHashCode(), Vector3.Distance(_transform.Position, cameraPosition));
        }

        public override RenderPasses RenderPasses => RenderPasses.ShadowMap | RenderPasses.Standard;

        private RasterizerState _rasterizerState;
        private bool _faceCullingChanged = false;
        private FaceCullingMode _faceCullingMode = FaceCullingMode.Back;

        public FaceCullingMode FaceCulling { get => _faceCullingMode; set { _faceCullingMode = value; _faceCullingChanged = true; } }

        public override void Render(RenderContext rc, SceneContext sc, RenderPasses renderPass)
        {
            if (_faceCullingChanged)
            {
                _faceCullingChanged = false;
                _rasterizerState?.Dispose();
                CreateRasterizerState(rc);
            }

            rc.SetRasterizerState(_rasterizerState ?? rc.DefaultRasterizerState);

            if (renderPass == RenderPasses.ShadowMap)
            {
                RenderShadowMap(rc, sc);
            }
            else if (renderPass == RenderPasses.Standard)
            {
                RenderStandard(rc, sc);
            }
        }

        private void CreateRasterizerState(RenderContext rc)
        {
            _rasterizerState = rc.ResourceFactory.CreateRasterizerState(
                _faceCullingMode,
                rc.DefaultRasterizerState.FillMode,
                rc.DefaultRasterizerState.IsDepthClipEnabled,
                rc.DefaultRasterizerState.IsScissorTestEnabled);
        }

        private void RenderShadowMap(RenderContext rc, SceneContext sc)
        {
            Matrix4x4 world = _transform.GetTransformMatrix();
            _worldBuffer.SetData(ref world);
            _inverseTransposeWorldBuffer.SetData(Utilities.CalculateInverseTranspose(ref world));

            rc.VertexBuffer = _vb;
            rc.IndexBuffer = _ib;
            rc.ShaderSet = _mapSet;
            rc.ShaderResourceBindingSlots = _mapSlots;
            rc.SetConstantBuffer(0, sc.CurrentLightViewProjectionBuffer);
            rc.SetConstantBuffer(1, _worldBuffer);
            rc.DrawIndexedPrimitives(_indexCount);
        }

        private void RenderStandard(RenderContext rc, SceneContext sc)
        {
            Matrix4x4 world = _transform.GetTransformMatrix();
            _worldBuffer.SetData(ref world);
            _inverseTransposeWorldBuffer.SetData(Utilities.CalculateInverseTranspose(ref world));

            rc.VertexBuffer = _vb;
            rc.IndexBuffer = _ib;
            rc.ShaderSet = _shaderSet;
            rc.ShaderResourceBindingSlots = _resourceSlots;
            int index = 0;
            rc.SetConstantBuffer(index++, sc.ProjectionMatrixBuffer);
            rc.SetConstantBuffer(index++, sc.ViewMatrixBuffer);
            rc.SetConstantBuffer(index++, _worldBuffer);
            rc.SetConstantBuffer(index++, _inverseTransposeWorldBuffer);
            rc.SetConstantBuffer(index++, sc.LightViewProjectionBuffer0);
            rc.SetConstantBuffer(index++, sc.LightViewProjectionBuffer1);
            rc.SetConstantBuffer(index++, sc.LightViewProjectionBuffer2);
            rc.SetConstantBuffer(index++, sc.DepthLimitsBuffer);
            rc.SetConstantBuffer(index++, sc.LightInfoBuffer);
            rc.SetConstantBuffer(index++, sc.CameraInfoBuffer);
            rc.SetConstantBuffer(index++, sc.PointLightsBuffer);
            rc.SetConstantBuffer(index++, _materialProps.ConstantBuffer);
            rc.SetTexture(index++, _textureBinding);
            rc.SetSamplerState(index++, rc.Anisox4Sampler);
            rc.SetTexture(index++, _alphamapBinding);
            rc.SetSamplerState(index++, rc.LinearSampler);
            rc.SetTexture(index++, sc.NearShadowMapBinding);
            rc.SetTexture(index++, sc.MidShadowMapBinding);
            rc.SetTexture(index++, sc.FarShadowMapBinding);
            rc.SetSamplerState(index++, rc.PointSampler);

            rc.DrawIndexedPrimitives(_indexCount);
        }
    }
}
