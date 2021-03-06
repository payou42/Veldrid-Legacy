﻿using System;
using OpenTK.Graphics;
using Veldrid.Platform;
using OpenTK.Graphics.ES30;
using System.Runtime.InteropServices;
using OpenTK;
using System.Diagnostics;

namespace Veldrid.Graphics.OpenGLES
{
    public class OpenGLESRenderContext : RenderContext
    {
        private readonly GraphicsContext _openGLGraphicsContext;
        private readonly OpenGLESDefaultFramebuffer _defaultFramebuffer;
        private readonly int _maxConstantBufferSlots;
        private readonly OpenGLESConstantBuffer[] _constantBuffersBySlot;
        private readonly OpenGLESConstantBuffer[] _newConstantBuffersBySlot; // CB's bound during draw call preparation
        private readonly OpenGLESTextureSamplerManager _textureSamplerManager;

        private int _newConstantBuffersCount;
        private PrimitiveType _primitiveType = PrimitiveType.Triangles;
        private int _vertexAttributesBound;
        private bool _vertexLayoutChanged;
        private int _baseVertexOffset = 0;
        private Action _swapBufferFunc;
        private DebugProc _debugMessageCallback;

        public DebugSeverity MinimumLogSeverity { get; set; } = DebugSeverity.DebugSeverityLow;

        public OpenGLESRenderContext(Window window, OpenGLPlatformContextInfo platformContext)
        {
            ResourceFactory = new OpenGLESResourceFactory();
            RenderCapabilities = new RenderCapabilities(false, false);
            _swapBufferFunc = platformContext.SwapBuffer;
            GraphicsContext.GetAddressDelegate getAddressFunc = s => platformContext.GetProcAddress(s);
            GraphicsContext.GetCurrentContextDelegate getCurrentContextFunc = () => new ContextHandle(platformContext.GetCurrentContext());
            _openGLGraphicsContext = new GraphicsContext(new ContextHandle(platformContext.ContextHandle), getAddressFunc, getCurrentContextFunc);

            _openGLGraphicsContext.LoadAll();

            _defaultFramebuffer = new OpenGLESDefaultFramebuffer(window.Width, window.Height);
            OnWindowResized(window.Width, window.Height);

            _textureSamplerManager = new OpenGLESTextureSamplerManager();

            _maxConstantBufferSlots = GL.GetInteger(GetPName.MaxUniformBufferBindings);
            Utilities.CheckLastGLES3Error();
            _constantBuffersBySlot = new OpenGLESConstantBuffer[_maxConstantBufferSlots];
            _newConstantBuffersBySlot = new OpenGLESConstantBuffer[_maxConstantBufferSlots];

            SetInitialStates();

            PostContextCreated();
        }

        public void EnableDebugCallback() => EnableDebugCallback(DebugSeverity.DebugSeverityLow);
        public void EnableDebugCallback(DebugSeverity minimumSeverity) => EnableDebugCallback(DefaultDebugCallback(minimumSeverity));
        public void EnableDebugCallback(DebugProc callback)
        {
            // The debug callback delegate must be persisted, otherwise errors will occur
            // when the OpenGL drivers attempt to call it after it has been collected.
            _debugMessageCallback = callback;
            GL.DebugMessageCallback(_debugMessageCallback, IntPtr.Zero);
        }

        private DebugProc DefaultDebugCallback(DebugSeverity minimumSeverity)
        {
            return (DebugSource source, DebugType type, int id, DebugSeverity severity, int length, IntPtr message, IntPtr userParam) =>
            {
                if (severity >= minimumSeverity)
                {
                    string messageString = Marshal.PtrToStringAnsi(message, length);
                    System.Diagnostics.Debug.WriteLine($"GL DEBUG MESSAGE: {source}, {type}, {id}. {severity}: {messageString}");
                }
            };
        }

        protected override GraphicsBackend PlatformGetGraphicsBackend() => GraphicsBackend.OpenGLES;

        public override ResourceFactory ResourceFactory { get; }

        public override RgbaFloat ClearColor
        {
            get
            {
                return base.ClearColor;
            }
            set
            {
                base.ClearColor = value;
                Color4 openTKColor = new Color4(value.R, value.G, value.B, value.A);
                GL.ClearColor(openTKColor);
                Utilities.CheckLastGLES3Error();
            }
        }

        protected override void PlatformClearBuffer()
        {
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            Utilities.CheckLastGLES3Error();
        }

        protected override void PlatformSwapBuffers()
        {
            if (_swapBufferFunc != null)
            {
                _swapBufferFunc();
            }
            else
            {
                _openGLGraphicsContext.SwapBuffers();
            }
        }

        public override void DrawIndexedPrimitives(int count, int startingIndex)
        {
            SetBaseVertexOffset(0);
            PreDrawCommand();
            var elementsType = ((OpenGLESIndexBuffer)IndexBuffer).ElementsType;
            int indexSize = OpenGLESFormats.GetIndexFormatSize(elementsType);

            GL.DrawElements(_primitiveType, count, elementsType, new IntPtr(startingIndex * indexSize));
            Utilities.CheckLastGLES3Error();
        }

        public override void DrawIndexedPrimitives(int count, int startingIndex, int startingVertex)
        {
            SetBaseVertexOffset(startingVertex);
            PreDrawCommand();
            var elementsType = ((OpenGLESIndexBuffer)IndexBuffer).ElementsType;
            int indexSize = OpenGLESFormats.GetIndexFormatSize(elementsType);
            GL.DrawElements(_primitiveType, count, elementsType, new IntPtr(startingIndex * indexSize));
        }

        public override void DrawInstancedPrimitives(int indexCount, int instanceCount, int startingIndex)
        {
            SetBaseVertexOffset(0);
            PreDrawCommand();
            var elementsType = ((OpenGLESIndexBuffer)IndexBuffer).ElementsType;
            int indexSize = OpenGLESFormats.GetIndexFormatSize(elementsType);
            GL.DrawElementsInstanced(_primitiveType, indexCount, elementsType, new IntPtr(startingIndex * indexSize), instanceCount);
            Utilities.CheckLastGLES3Error();
        }

        public override void DrawInstancedPrimitives(int indexCount, int instanceCount, int startingIndex, int startingVertex)
        {
            SetBaseVertexOffset(startingVertex);
            PreDrawCommand();
            var elementsType = ((OpenGLESIndexBuffer)IndexBuffer).ElementsType;
            int indexSize = OpenGLESFormats.GetIndexFormatSize(elementsType);
            GL.DrawElementsInstanced(
                _primitiveType,
                indexCount,
                elementsType,
                new IntPtr(startingIndex * indexSize),
                instanceCount);
        }

        private void SetBaseVertexOffset(int offset)
        {
            if (_baseVertexOffset != offset)
            {
                _baseVertexOffset = offset;
                _vertexLayoutChanged = true;
            }
        }

        private void SetInitialStates()
        {
            GL.ClearColor(ClearColor.R, ClearColor.G, ClearColor.B, ClearColor.A);
            Utilities.CheckLastGLES3Error();
            GL.Enable(EnableCap.CullFace);
            Utilities.CheckLastGLES3Error();
            GL.FrontFace(FrontFaceDirection.Cw);
            Utilities.CheckLastGLES3Error();
        }

        protected override void PlatformResize(int width, int height)
        {
            _defaultFramebuffer.Width = width;
            _defaultFramebuffer.Height = height;
        }

        protected override void PlatformSetViewport(int x, int y, int width, int height)
        {
            GL.Viewport(x, y, width, height);
            Utilities.CheckLastGLES3Error();
        }

        protected override void PlatformSetPrimitiveTopology(PrimitiveTopology primitiveTopology)
        {
            _primitiveType = OpenGLESFormats.ConvertPrimitiveTopology(primitiveTopology);
        }

        protected override void PlatformSetDefaultFramebuffer()
        {
            SetFramebuffer(_defaultFramebuffer);
        }

        protected override void PlatformSetScissorRectangle(Rectangle rectangle)
        {
            GL.Enable(EnableCap.ScissorTest);
            Utilities.CheckLastGLES3Error();
            GL.Scissor(
                rectangle.Left,
                Viewport.Height - rectangle.Bottom,
                rectangle.Width,
                rectangle.Height);
        }

        public override void ClearScissorRectangle()
        {
            GL.Disable(EnableCap.ScissorTest);
            Utilities.CheckLastGLES3Error();
        }

        protected override void PlatformSetVertexBuffer(int slot, VertexBuffer vb)
        {
            _vertexLayoutChanged = true;
        }

        protected override void PlatformSetIndexBuffer(IndexBuffer ib)
        {
            ((OpenGLESIndexBuffer)ib).Apply();
            _vertexLayoutChanged = true;
        }

        protected override void PlatformSetShaderSet(ShaderSet shaderSet)
        {
            OpenGLESShaderSet glShaderSet = (OpenGLESShaderSet)shaderSet;
            GL.UseProgram(glShaderSet.ProgramID);
            Utilities.CheckLastGLES3Error();
            _vertexLayoutChanged = true;
        }

        protected override void PlatformSetShaderResourceBindingSlots(ShaderResourceBindingSlots shaderConstantBindings)
        {
        }

        protected override void PlatformSetConstantBuffer(int slot, ConstantBuffer cb)
        {
            OpenGLESUniformBinding binding = ShaderResourceBindingSlots.GetUniformBindingForSlot(slot);

            ((OpenGLESConstantBuffer)cb).BindToBlock(ShaderSet.ProgramID, binding.BlockLocation, ((OpenGLESConstantBuffer)cb).BufferSize, slot);
            //if (binding.BlockLocation != -1)
            //{
            //    BindUniformBlock(ShaderSet, slot, binding.BlockLocation, (OpenGLESConstantBuffer)cb);
            //}
            //else
            //{
            //    Debug.Assert(binding.StorageAdapter != null);
            //    SetUniformLocationDataSlow((OpenGLESConstantBuffer)cb, binding.StorageAdapter);
            //}
        }

        private void BindUniformBlock(OpenGLESShaderSet shaderSet, int slot, int blockLocation, OpenGLESConstantBuffer cb)
        {
            if (slot > _maxConstantBufferSlots)
            {
                throw new VeldridException($"Too many constant buffers used. Limit is {_maxConstantBufferSlots}.");
            }

            // Bind Constant Buffer to slot
            if (_constantBuffersBySlot[slot] == cb)
            {
                if (_newConstantBuffersBySlot[slot] != null)
                {
                    _newConstantBuffersCount -= 1;
                }
                _newConstantBuffersBySlot[slot] = null;
            }
            else
            {
                if (_newConstantBuffersBySlot[slot] == null)
                {
                    _newConstantBuffersCount += 1;
                }

                _newConstantBuffersBySlot[slot] = cb;
            }

            // Bind slot to uniform block location. Performs internal caching to avoid GL calls.
            shaderSet.BindConstantBuffer(slot, blockLocation, cb);
        }

        private void CommitNewConstantBufferBindings()
        {
            if (_newConstantBuffersCount > 0)
            {
                CommitNewConstantBufferBindings_SingleBind();
                Array.Clear(_newConstantBuffersBySlot, 0, _newConstantBuffersBySlot.Length);
                _newConstantBuffersCount = 0;
            }
        }

        private void CommitNewConstantBufferBindings_SingleBind()
        {
            int remainingBindings = _newConstantBuffersCount;
            for (int slot = 0; slot < _maxConstantBufferSlots; slot++)
            {
                if (remainingBindings == 0)
                {
                    return;
                }

                OpenGLESConstantBuffer cb = _newConstantBuffersBySlot[slot];
                if (cb != null)
                {
                    GL.BindBufferRange(BufferRangeTarget.UniformBuffer, slot, cb.BufferID, IntPtr.Zero, cb.BufferSize);
                    Utilities.CheckLastGLES3Error();
                    remainingBindings -= 1;
                }
            }
        }

        private unsafe void SetUniformLocationDataSlow(OpenGLESConstantBuffer cb, OpenGLESUniformStorageAdapter storageAdapter)
        {
            // NOTE: This is slow -- avoid using uniform locations in shader code. Prefer uniform blocks.
            int dataSizeInBytes = cb.BufferSize;
            byte* data = stackalloc byte[dataSizeInBytes];
            cb.GetData((IntPtr)data, dataSizeInBytes);
            storageAdapter.SetData((IntPtr)data, dataSizeInBytes);
        }

        protected override void PlatformSetTexture(int slot, ShaderTextureBinding textureBinding)
        {
            OpenGLESTextureBinding glTextureBinding = (OpenGLESTextureBinding)textureBinding;
            OpenGLESTextureBindingSlotInfo info = ShaderResourceBindingSlots.GetTextureBindingInfo(slot);
            _textureSamplerManager.SetTexture(info.RelativeIndex, glTextureBinding);
            ShaderSet.UpdateTextureUniform(info.UniformLocation, info.RelativeIndex);
        }

        protected override void PlatformSetSamplerState(int slot, SamplerState samplerState)
        {
            OpenGLESSamplerState glSampler = (OpenGLESSamplerState)samplerState;
            OpenGLESTextureBindingSlotInfo info = ShaderResourceBindingSlots.GetSamplerBindingInfo(slot);
            _textureSamplerManager.SetSampler(info.RelativeIndex, glSampler);
        }

        protected override void PlatformSetFramebuffer(Framebuffer framebuffer)
        {
            OpenGLESFramebufferBase baseFramebuffer = (OpenGLESFramebufferBase)framebuffer;
            if (baseFramebuffer is OpenGLESFramebuffer)
            {
                OpenGLESFramebuffer glFramebuffer = (OpenGLESFramebuffer)baseFramebuffer;
                if (!glFramebuffer.HasDepthAttachment || !DepthStencilState.IsDepthEnabled)
                {
                    GL.Disable(EnableCap.DepthTest);
                    Utilities.CheckLastGLES3Error();
                    GL.DepthMask(false);
                    Utilities.CheckLastGLES3Error();
                }
                else
                {
                    GL.Enable(EnableCap.DepthTest);
                    Utilities.CheckLastGLES3Error();
                    GL.DepthMask(DepthStencilState.IsDepthWriteEnabled);
                    Utilities.CheckLastGLES3Error();
                }
            }

            baseFramebuffer.Apply();
        }

        protected override void PlatformSetBlendstate(BlendState blendState)
        {
            ((OpenGLESBlendState)blendState).Apply();
        }

        protected override void PlatformSetDepthStencilState(DepthStencilState depthStencilState)
        {
            ((OpenGLESDepthStencilState)depthStencilState).Apply();
        }

        protected override void PlatformSetRasterizerState(RasterizerState rasterizerState)
        {
            ((OpenGLESRasterizerState)rasterizerState).Apply();
        }

        protected override void PlatformClearMaterialResourceBindings()
        {
        }

        public override RenderCapabilities RenderCapabilities { get; }

        protected override void PlatformDispose()
        {
            _openGLGraphicsContext.Dispose();
        }

        protected override System.Numerics.Vector2 GetTopLeftUvCoordinate()
        {
            return new System.Numerics.Vector2(0, 1);
        }

        protected override System.Numerics.Vector2 GetBottomRightUvCoordinate()
        {
            return new System.Numerics.Vector2(1, 0);
        }

        private void PreDrawCommand()
        {
            if (_vertexLayoutChanged)
            {
                _vertexAttributesBound = ((OpenGLESVertexInputLayout)ShaderSet.InputLayout).SetVertexAttributes(VertexBuffers, _vertexAttributesBound, _baseVertexOffset);
                _vertexLayoutChanged = false;
            }

            CommitNewConstantBufferBindings();
        }

        private new OpenGLESShaderResourceBindingSlots ShaderResourceBindingSlots
            => (OpenGLESShaderResourceBindingSlots)base.ShaderResourceBindingSlots;

        private new OpenGLESShaderSet ShaderSet => (OpenGLESShaderSet)base.ShaderSet;
    }
}
