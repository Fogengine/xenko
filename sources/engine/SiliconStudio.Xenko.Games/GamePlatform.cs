﻿// Copyright (c) 2014 Silicon Studio Corp. (http://siliconstudio.co.jp)
// This file is distributed under GPL v3. See LICENSE.md for details.
//
// Copyright (c) 2010-2013 SharpDX - Alexandre Mutel
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;
using SiliconStudio.Xenko.Graphics;
using SiliconStudio.Core;

namespace SiliconStudio.Xenko.Games
{
    internal abstract class GamePlatform : ReferenceBase, IGraphicsDeviceFactory, IGamePlatform
    {
        private bool hasExitRan = false;

        protected readonly GameBase game;

        protected readonly IServiceRegistry Services;

        protected GameWindow gameWindow;

        protected GamePlatform(GameBase game)
        {
            this.game = game;
            Services = game.Services;
            Services.AddService(typeof(IGraphicsDeviceFactory), this);
        }

        public static GamePlatform Create(GameBase game)
        {
#if SILICONSTUDIO_PLATFORM_WINDOWS_RUNTIME
            return new GamePlatformWindowsRuntime(game);
#elif SILICONSTUDIO_PLATFORM_WINDOWS_DESKTOP && SILICONSTUDIO_XENKO_GRAPHICS_API_OPENGL
            return new GamePlatformOpenTK(game);
#elif SILICONSTUDIO_PLATFORM_ANDROID
            return new GamePlatformAndroid(game);
#elif SILICONSTUDIO_PLATFORM_IOS
            return new GamePlatformiOS(game);
#else
            return new GamePlatformDesktop(game);
#endif
        }

        public abstract string DefaultAppDirectory { get; }

        public object WindowContext { get; set; }

        public event EventHandler<EventArgs> Activated;

        public event EventHandler<EventArgs> Deactivated;

        public event EventHandler<EventArgs> Exiting;

        public event EventHandler<EventArgs> Idle;

        public event EventHandler<EventArgs> Resume;

        public event EventHandler<EventArgs> Suspend;

        public event EventHandler<EventArgs> WindowCreated;

        public GameWindow MainWindow
        {
            get
            {
                return gameWindow;
            }
        }

        internal abstract GameWindow[] GetSupportedGameWindows();

        public virtual GameWindow CreateWindow(GameContext gameContext)
        {
            gameContext = gameContext ?? new GameContext();

            var windows = GetSupportedGameWindows();

            foreach (var gameWindowToTest in windows)
            {
                if (gameWindowToTest.CanHandle(gameContext))
                {
                    gameWindowToTest.Services = Services;
                    gameWindowToTest.Initialize(gameContext);
                    return gameWindowToTest;
                }
            }

            throw new ArgumentException("Game Window context not supported on this platform");
        }

        public bool IsBlockingRun { get; protected set; }

        public void Run(GameContext gameContext)
        {
            gameWindow = CreateWindow(gameContext);

            // Register on Activated 
            gameWindow.GameContext = gameContext;
            gameWindow.Activated += OnActivated;
            gameWindow.Deactivated += OnDeactivated;
            gameWindow.InitCallback = OnInitCallback;
            gameWindow.RunCallback = OnRunCallback;

            var windowCreated = WindowCreated;
            if (windowCreated != null)
            {
                windowCreated(this, EventArgs.Empty);
            }

            gameWindow.Run();
        }

        private void OnRunCallback()
        {
            // If/else outside of try-catch to separate user-unhandled exceptions properly
            var unhandledException = game.UnhandledExceptionInternal;
            if (unhandledException != null)
            {
                // Catch exceptions and transmit them to UnhandledException event
                try
                {
                    Tick();
                }
                catch (Exception e)
                {
                    // Some system was listening for exceptions
                    unhandledException(this, new GameUnhandledExceptionEventArgs(e, false));
                    game.Exit();
                }
            }
            else
            {
                Tick();
            }
        }

        private void OnInitCallback()
        {
            // If/else outside of try-catch to separate user-unhandled exceptions properly
            var unhandledException = game.UnhandledExceptionInternal;
            if (unhandledException != null)
            {
                // Catch exceptions and transmit them to UnhandledException event
                try
                {
                    game.InitializeBeforeRun();
                }
                catch (Exception e)
                {
                    // Some system was listening for exceptions
                    unhandledException(this, new GameUnhandledExceptionEventArgs(e, false));
                    game.Exit();
                }
            }
            else
            {
                game.InitializeBeforeRun();
            }
        }

        private void Tick()
        {
            game.Tick();

            if (!IsBlockingRun && game.IsExiting() && !hasExitRan)
            {
                hasExitRan = true;
                OnExiting(this, EventArgs.Empty);
            }
        }

        public virtual void Exit()
        {
            // Notifies that the GameWindow should exit.
            gameWindow.Exiting = true;
        }

        protected void OnActivated(object source, EventArgs e)
        {
            EventHandler<EventArgs> handler = Activated;
            if (handler != null) handler(this, e);
        }

        protected void OnDeactivated(object source, EventArgs e)
        {
            EventHandler<EventArgs> handler = Deactivated;
            if (handler != null) handler(this, e);
        }

        protected void OnExiting(object source, EventArgs e)
        {
            EventHandler<EventArgs> handler = Exiting;
            if (handler != null) handler(this, e);
        }

        protected void OnIdle(object source, EventArgs e)
        {
            EventHandler<EventArgs> handler = Idle;
            if (handler != null) handler(this, e);
        }

        protected void OnResume(object source, EventArgs e)
        {
            EventHandler<EventArgs> handler = Resume;
            if (handler != null) handler(this, e);
        }

        protected void OnSuspend(object source, EventArgs e)
        {
            EventHandler<EventArgs> handler = Suspend;
            if (handler != null) handler(this, e);
        }

        protected void AddDevice(DisplayMode mode,  GraphicsDeviceInformation deviceBaseInfo, GameGraphicsParameters preferredParameters, List<GraphicsDeviceInformation> graphicsDeviceInfos)
        {
            // TODO: Temporary woraround
            if (mode == null)
                mode = new DisplayMode(PixelFormat.R8G8B8A8_UNorm, 800, 480, new Rational(60, 1));

            var deviceInfo = deviceBaseInfo.Clone();

            deviceInfo.PresentationParameters.RefreshRate = mode.RefreshRate;

            if (preferredParameters.IsFullScreen)
            {
                deviceInfo.PresentationParameters.BackBufferFormat = mode.Format;
                deviceInfo.PresentationParameters.BackBufferWidth = mode.Width;
                deviceInfo.PresentationParameters.BackBufferHeight = mode.Height;
            }
            else
            {
                deviceInfo.PresentationParameters.BackBufferFormat = preferredParameters.PreferredBackBufferFormat;
                deviceInfo.PresentationParameters.BackBufferWidth = preferredParameters.PreferredBackBufferWidth;
                deviceInfo.PresentationParameters.BackBufferHeight = preferredParameters.PreferredBackBufferHeight;
            }

            // TODO: Handle multisampling 
            deviceInfo.PresentationParameters.DepthStencilFormat = preferredParameters.PreferredDepthStencilFormat;
            deviceInfo.PresentationParameters.MultiSampleCount = MSAALevel.None;

            if (!graphicsDeviceInfos.Contains(deviceInfo))
            {
                graphicsDeviceInfos.Add(deviceInfo);
            }
        }

        public virtual List<GraphicsDeviceInformation> FindBestDevices(GameGraphicsParameters preferredParameters)
        {
            var graphicsDeviceInfos = new List<GraphicsDeviceInformation>();

            // Iterate on each adapter
            foreach (var graphicsAdapter in GraphicsAdapterFactory.Adapters)
            {
                // Skip adapeters that don't have graphics output
                if (graphicsAdapter.Outputs.Length == 0)
                {
                    continue;
                }

                var preferredGraphicsProfiles = preferredParameters.PreferredGraphicsProfile;

                // INTEL workaround: it seems Intel driver doesn't support properly feature level 9.x. Fallback to 10.
                if (graphicsAdapter.VendorId == 0x8086)
                    preferredGraphicsProfiles = preferredGraphicsProfiles.Select(x => x < GraphicsProfile.Level_10_0 ? GraphicsProfile.Level_10_0 : x).ToArray();

                // Iterate on each preferred graphics profile
                foreach (var featureLevel in preferredGraphicsProfiles)
                {
                    // Check if this profile is supported.
                    if (graphicsAdapter.IsProfileSupported(featureLevel))
                    {
                        var deviceInfo = new GraphicsDeviceInformation
                            {
                                Adapter = graphicsAdapter,
                                GraphicsProfile = featureLevel,
                                PresentationParameters =
                                    {
                                        MultiSampleCount = MSAALevel.None,
                                        IsFullScreen = preferredParameters.IsFullScreen,
                                        PreferredFullScreenOutputIndex = preferredParameters.PreferredFullScreenOutputIndex,
                                        PresentationInterval = preferredParameters.SynchronizeWithVerticalRetrace ? PresentInterval.One : PresentInterval.Immediate,
                                        DeviceWindowHandle = MainWindow.NativeWindow,
                                        ColorSpace = preferredParameters.ColorSpace
                                    }
                            };

                        var preferredMode = new DisplayMode(preferredParameters.PreferredBackBufferFormat,
                            preferredParameters.PreferredBackBufferWidth,
                            preferredParameters.PreferredBackBufferHeight,
                            preferredParameters.PreferredRefreshRate);

                        // if we want to switch to fullscreen, try to find only needed output, otherwise check them all
                        if (preferredParameters.IsFullScreen)
                        {
                            if (preferredParameters.PreferredFullScreenOutputIndex < graphicsAdapter.Outputs.Length)
                            {
                                var output = graphicsAdapter.Outputs[preferredParameters.PreferredFullScreenOutputIndex];
                                var displayMode = output.FindClosestMatchingDisplayMode(preferredGraphicsProfiles, preferredMode);
                                AddDevice(displayMode, deviceInfo, preferredParameters, graphicsDeviceInfos);
                            }
                        }
                        else
                        {
                            AddDevice(preferredMode, deviceInfo, preferredParameters, graphicsDeviceInfos);
                        }

                        // If the profile is supported, we are just using the first best one
                        break;
                    }
                }
            }

            return graphicsDeviceInfos;
        }

        public virtual GraphicsDevice CreateDevice(GraphicsDeviceInformation deviceInformation)
        {
            var graphicsDevice = GraphicsDevice.New(deviceInformation.Adapter, deviceInformation.DeviceCreationFlags, gameWindow.NativeWindow, deviceInformation.GraphicsProfile);
            graphicsDevice.ColorSpace = deviceInformation.PresentationParameters.ColorSpace;
            graphicsDevice.Presenter = new SwapChainGraphicsPresenter(graphicsDevice, deviceInformation.PresentationParameters);

            return graphicsDevice;
        }

        public virtual void RecreateDevice(GraphicsDevice currentDevice, GraphicsDeviceInformation deviceInformation)
        {
            currentDevice.ColorSpace = deviceInformation.PresentationParameters.ColorSpace;
            currentDevice.Recreate(deviceInformation.Adapter ?? GraphicsAdapterFactory.Default, new[] { deviceInformation.GraphicsProfile }, deviceInformation.DeviceCreationFlags, gameWindow.NativeWindow);
        }

        public virtual void DeviceChanged(GraphicsDevice currentDevice, GraphicsDeviceInformation deviceInformation)
        {
            // Force to resize the gameWindow
            gameWindow.Resize(deviceInformation.PresentationParameters.BackBufferWidth, deviceInformation.PresentationParameters.BackBufferHeight);
        }

        public virtual GraphicsDevice ChangeOrCreateDevice(GraphicsDevice currentDevice, GraphicsDeviceInformation deviceInformation)
        {
            if (currentDevice == null)
            {
                currentDevice = CreateDevice(deviceInformation);
            }
            else
            {
                RecreateDevice(currentDevice, deviceInformation);
            }

            DeviceChanged(currentDevice, deviceInformation);

            return currentDevice;
        }

        protected override void Destroy()
        {
            if (gameWindow != null)
            {
                gameWindow.Dispose();
                gameWindow = null;
            }

            Activated = null;
            Deactivated = null;
            Exiting = null;
            Idle = null;
            Resume = null;
            Suspend = null;
        }
    }
}