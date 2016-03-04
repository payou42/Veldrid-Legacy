﻿using System;
using System.Numerics;

namespace Veldrid.Graphics
{
    public abstract class RenderContext
    {
        private readonly OpenTKWindowInfo _windowInfo;
        private readonly float _fieldOfViewRadians = 1.05f;
        private readonly DedicatdThreadWindow _window;

        private VertexBuffer _vertexBuffer;
        private IndexBuffer _indexBuffer;
        private Material _material;

        public RenderContext()
        {
            _window = new DedicatdThreadWindow();
        }

        public WindowInfo WindowInfo => _window.WindowInfo;

        public WindowInputProvider InputProvider => _window;

        public virtual RgbaFloat ClearColor { get; set; } = RgbaFloat.CornflowerBlue;

        public abstract ResourceFactory ResourceFactory { get; }

        public DynamicDataProvider<Matrix4x4> ViewMatrixProvider { get; }
            = new DynamicDataProvider<Matrix4x4>(
                Matrix4x4.CreateLookAt(new Vector3(0, 3, 5), new Vector3(0, 0, 0), new Vector3(0, 1, 0)));

        public DynamicDataProvider<Matrix4x4> ProjectionMatrixProvider { get; }
            = new DynamicDataProvider<Matrix4x4>();

        public event Action WindowResized;

        public void SetVertexBuffer(VertexBuffer vb)
        {
            if (vb != _vertexBuffer)
            {
                vb.Apply();
                _vertexBuffer = vb;
            }
        }

        public void SetIndexBuffer(IndexBuffer ib)
        {
            if (ib != _indexBuffer)
            {
                ib.Apply();
                _indexBuffer = ib;
            }
        }

        public void SetMaterial(Material material)
        {
            if (material != _material)
            {
                material.Apply();
                _material = material;
            }
        }

        public abstract void DrawIndexedPrimitives(int startingIndex, int indexCount);

        public void ClearBuffer()
        {
            if (_window.NeedsResizing)
            {
                OnWindowResized();
                _window.NeedsResizing = false;
            }

            PlatformClearBuffer();
            NullInputs();
        }

        private void NullInputs()
        {
            _vertexBuffer = null;
            _indexBuffer = null;
            _material = null;
        }

        public void SwapBuffers()
        {
            PlatformSwapBuffers();
        }

        protected OpenTK.NativeWindow NativeWindow => _window.NativeWindow;

        protected void OnWindowResized()
        {
            ProjectionMatrixProvider.Data = Matrix4x4.CreatePerspectiveFieldOfView(
                _fieldOfViewRadians,
                WindowInfo.Width / (float)WindowInfo.Height,
                1f,
                1000f);

            PlatformResize();
            WindowResized?.Invoke();
        }

        protected abstract void PlatformClearBuffer();

        protected abstract void PlatformSwapBuffers();

        protected abstract void PlatformResize();
    }
}
