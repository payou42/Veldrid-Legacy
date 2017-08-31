﻿using System;
using System.Numerics;
using Veldrid.Platform;

namespace Veldrid.NeoDemo
{
    public class Camera : IUpdateable
    {
        private readonly SceneContext _sc;

        private float _fov = 1f;
        private float _near = 0.5f;
        private float _far = 1000f;

        private Vector3 _position = new Vector3(0, 3, 0);
        private Vector3 _lookDirection = new Vector3(0, -.3f, -1f);
        private float _moveSpeed = 1f;
        private Matrix4x4 _viewMatrix;

        private float _yaw;
        private float _pitch;

        private Vector2 _previousMousePos;

        public Camera(SceneContext sc, float width, float height)
        {
            _sc = sc;
            UpdatePerspectiveMatrix(width, height);
            UpdateViewMatrix();
        }

        public Matrix4x4 ViewMatrix => _viewMatrix;

        public void Update(float deltaSeconds)
        {
            Vector3 motionDir = Vector3.Zero;
            if (InputTracker.GetKey(Key.A))
            {
                motionDir += -Vector3.UnitX;
            }
            if (InputTracker.GetKey(Key.D))
            {
                motionDir += Vector3.UnitX;
            }
            if (InputTracker.GetKey(Key.W))
            {
                motionDir += -Vector3.UnitZ;
            }
            if (InputTracker.GetKey(Key.S))
            {
                motionDir += Vector3.UnitZ;
            }
            if (InputTracker.GetKey(Key.Q))
            {
                motionDir += -Vector3.UnitY;
            }
            if (InputTracker.GetKey(Key.E))
            {
                motionDir += Vector3.UnitY;
            }

            if (motionDir != Vector3.Zero)
            {
                Quaternion lookRotation = Quaternion.CreateFromYawPitchRoll(_yaw, _pitch, 0f);
                motionDir = Vector3.Transform(motionDir, lookRotation);
                _position += motionDir * _moveSpeed * deltaSeconds;
                UpdateViewMatrix();
            }

            Vector2 mouseDelta = InputTracker.MousePosition - _previousMousePos;
            _previousMousePos = InputTracker.MousePosition;

            if (InputTracker.GetMouseButton(MouseButton.Left) || InputTracker.GetMouseButton(MouseButton.Right))
            {
                _yaw += -mouseDelta.X * 0.01f;
                _pitch += -mouseDelta.Y * 0.01f;
                _pitch = Math.Clamp(_pitch, -1.55f, 1.55f);

                Quaternion lookRotation = Quaternion.CreateFromYawPitchRoll(_yaw, _pitch, 0f);
                Vector3 lookDir = Vector3.Transform(-Vector3.UnitZ, lookRotation);
                _lookDirection = lookDir;
                UpdateViewMatrix();
            }
        }

        public void WindowResized(float width, float height)
        {
            UpdatePerspectiveMatrix(width, height);
        }

        private void UpdatePerspectiveMatrix(float width, float height)
        {
            Matrix4x4 perspective = Matrix4x4.CreatePerspectiveFieldOfView(_fov, width / height, _near, _far);
            _sc.ProjectionMatrixBuffer.SetData(ref perspective);
        }

        private void UpdateViewMatrix()
        {
            _viewMatrix = Matrix4x4.CreateLookAt(_position, _position + _lookDirection, Vector3.UnitY);
            _sc.ViewMatrixBuffer.SetData(ref _viewMatrix);
        }
    }
}
