﻿using ImGuiNET;
using System.Collections.Generic;
using Veldrid.Graphics;
using Veldrid.NeoDemo.Objects;
using Veldrid.Platform;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;

namespace Veldrid.NeoDemo
{
    public class NeoDemo
    {
        private Sdl2Window _window;
        private RenderContext _rc;
        private readonly List<Renderable> _renderables = new List<Renderable>();
        private readonly List<IUpdateable> _updateables = new List<IUpdateable>();
        private readonly ImGuiRenderable _igRenderable;
        private readonly SceneContext _sc = new SceneContext();
        private readonly Camera _camera;
        private bool _windowResized;

        public NeoDemo()
        {
            WindowCreateInfo windowCI = new WindowCreateInfo
            {
                WindowWidth = 960,
                WindowHeight = 540,
                WindowInitialState = WindowState.Normal,
                WindowTitle = "Veldrid NeoDemo"
            };
            RenderContextCreateInfo rcCI = new RenderContextCreateInfo();

            VeldridStartup.CreateWindowAndRenderContext(ref windowCI, ref rcCI, out _window, out _rc);
            _window.Resized += () => _windowResized = true;

            _sc.CreateDeviceObjects(_rc);

            _camera = new Camera(_sc, _window.Width, _window.Height);
            _updateables.Add(_camera);
            _sc.Camera = _camera;

            _igRenderable = new ImGuiRenderable(_window);
            _igRenderable.CreateDeviceObjects(_rc);
            _renderables.Add(_igRenderable);
            _updateables.Add(_igRenderable);

            InfiniteGrid grid = new InfiniteGrid();
            grid.CreateDeviceObjects(_rc);
            _renderables.Add(grid);

            Skybox skybox = Skybox.LoadDefaultSkybox();
            skybox.CreateDeviceObjects(_rc);
            _renderables.Add(skybox);
        }

        public void Run()
        {
            while (_window.Exists)
            {
                InputTracker.UpdateFrameInput(_window.PumpEvents());
                Update(1f / 60f);
                Draw();
            }
        }

        private void Update(float deltaSeconds)
        {
            foreach (IUpdateable updateable in _updateables)
            {
                updateable.Update(deltaSeconds);
            }

            if (ImGui.BeginMainMenuBar())
            {
                if (ImGui.BeginMenu("Settings"))
                {
                    if (ImGui.BeginMenu("Graphics Backend"))
                    {
                        if (ImGui.MenuItem("Vulkan"))
                        {
                            ChangeRenderContext(GraphicsBackend.Vulkan);
                        }
                        if (ImGui.MenuItem("OpenGL"))
                        {
                            ChangeRenderContext(GraphicsBackend.OpenGL);
                        }
                        if (ImGui.MenuItem("OpenGL ES"))
                        {
                            ChangeRenderContext(GraphicsBackend.OpenGLES);
                        }
                        if (ImGui.MenuItem("Direct3D 11"))
                        {
                            ChangeRenderContext(GraphicsBackend.Direct3D11);
                        }
                        ImGui.EndMenu();
                    }
                    ImGui.EndMenu();
                }
                ImGui.EndMainMenuBar();
            }

            _window.Title = _rc.BackendType.ToString();
        }

        private void Draw()
        {
            if (_windowResized)
            {
                _windowResized = false;
                _rc.ResizeMainWindow(_window.Width, _window.Height);
                _camera.WindowResized(_window.Width, _window.Height);
            }

            _rc.Viewport = new Viewport(0, 0, _window.Width, _window.Height);
            _rc.ClearBuffer(RgbaFloat.CornflowerBlue);

            foreach (Renderable renderable in _renderables)
            {
                renderable.Render(_rc, _sc);
            }

            _rc.SwapBuffers();
        }

        private void ChangeRenderContext(GraphicsBackend backend)
        {
            _sc.DestroyDeviceObjects();
            foreach (Renderable renderable in _renderables)
            {
                renderable.DestroyDeviceObjects();
            }

            _rc.Dispose();

            RenderContextCreateInfo rcCI = new RenderContextCreateInfo
            {
                Backend = backend
            };
            _rc = VeldridStartup.CreateRenderContext(ref rcCI, _window);

            _sc.CreateDeviceObjects(_rc);
            foreach (Renderable renderable in _renderables)
            {
                renderable.CreateDeviceObjects(_rc);
            }
        }
    }
}
