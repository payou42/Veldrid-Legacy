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
        private ConstantBuffer _worldBuffer;
        private ConstantBuffer _inverseTransposeWorldBuffer;
        private DeviceTexture _texture;
        private ShaderTextureBinding _textureBinding;

        private ShaderSet _mapSet;
        private ShaderResourceBindingSlots _mapSlots;

        public Transform Transform => _transform;

        public TexturedMesh(MeshData meshData, TextureData textureData)
        {
            _meshData = meshData;
            _centeredBounds = meshData.GetBoundingBox();
            _textureData = textureData;
        }

        public override BoundingBox BoundingBox => BoundingBox.Transform(_centeredBounds, _transform.GetTransformMatrix());

        public override void CreateDeviceObjects(RenderContext rc)
        {
            ResourceFactory factory = rc.ResourceFactory;
            _vb = _meshData.CreateVertexBuffer(factory);
            _ib = _meshData.CreateIndexBuffer(factory, out _indexCount);

            TexturedMeshSetInfo.CreateAll(
                factory,
                ShaderHelper.LoadBytecode(factory, "TexturedMesh", ShaderStages.Vertex),
                ShaderHelper.LoadBytecode(factory, "TexturedMesh", ShaderStages.Fragment),
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
            _texture = _textureData.CreateDeviceTexture(factory);
            _textureBinding = factory.CreateShaderTextureBinding(_texture);
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
        }

        public override RenderOrderKey GetRenderOrderKey(Vector3 cameraPosition)
        {
            return RenderOrderKey.Create(_shaderSet.GetHashCode(), Vector3.Distance(_transform.Position, cameraPosition));
        }

        public override void Render(RenderContext rc, SceneContext sc, RenderPasses renderPass)
        {
            if (renderPass == RenderPasses.ShadowMap)
            {
                RenderShadowMap(rc, sc);
            }
            else if (renderPass == RenderPasses.Standard)
            {
                RenderStandard(rc, sc);
            }
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
            rc.SetConstantBuffer(0, sc.LightProjectionBuffer);
            rc.SetConstantBuffer(1, sc.LightViewBuffer);
            rc.SetConstantBuffer(2, _worldBuffer);
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
            rc.SetConstantBuffer(0, sc.ProjectionMatrixBuffer);
            rc.SetConstantBuffer(1, sc.ViewMatrixBuffer);
            rc.SetConstantBuffer(2, _worldBuffer);
            rc.SetConstantBuffer(3, _inverseTransposeWorldBuffer);
            rc.SetConstantBuffer(4, sc.LightInfoBuffer);
            rc.SetTexture(5, _textureBinding);
            rc.SetSamplerState(6, rc.PointSampler);
            rc.DrawIndexedPrimitives(_indexCount);
        }
    }
}