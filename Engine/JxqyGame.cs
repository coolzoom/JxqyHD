using System;
using System.Linq;
using System.Windows.Forms;
using Engine.Benchmark;
using Engine.Gui;
using Engine.ListManager;
using Engine.Script;
using Engine.Weather;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Keys = Microsoft.Xna.Framework.Input.Keys;

namespace Engine
{
    /// <summary>
    /// This is the main type for your game
    /// </summary>
    public class JxqyGame : Microsoft.Xna.Framework.Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private Effect _waterEffect;
        private Texture2D _waterfallTexture;
        private RenderTarget2D _renderTarget;

        private double _totalInactivedSeconds;
        private const double MaxInactivedSeconds = 5.0;
        private bool _isInactivedReachMaxInterval;

        private readonly Color BackgroundColor = Color.Black;

        IntPtr _drawSurface;
        Form _parentForm;
        PictureBox _pictureBox;
        Control _gameForm;

        public bool IsInEditMode { private set; get; }
        public bool IsPaused { get; set; }
        public bool IsGamePlayPaused { get; set; }
        public KeyboardState LastKeyboardState { private set; get; }
        public MouseState LastMouseState { private set; get; }

        /// <summary>
        /// Indicates weather game window is lost focus.
        /// Is game is run in edit mode, the value is always flase.
        /// </summary>
        public bool IsFocusLost
        {
            get { return (!IsInEditMode && _isInactivedReachMaxInterval); }
        }

        public JxqyGame()
        {
            _graphics = new GraphicsDeviceManager(this);
            _graphics.GraphicsProfile = GraphicsProfile.HiDef;
            Content.RootDirectory = "Content";
            IsMouseVisible = false;
            _graphics.IsFullScreen = false;
            GameState.State = GameState.StateType.Start;
        }

        public JxqyGame(IntPtr drawSurface, Form parentForm, PictureBox surfacePictureBox)
            : this()
        {
            IsInEditMode = true;
            _drawSurface = drawSurface;
            _parentForm = parentForm;
            _pictureBox = surfacePictureBox;

            _graphics.PreparingDeviceSettings += graphics_PreparingDeviceSettings;
            Mouse.WindowHandle = drawSurface;

            _gameForm = Control.FromHandle(Window.Handle);
            _gameForm.VisibleChanged += gameForm_VisibleChanged;
            _pictureBox.SizeChanged += pictureBox_SizeChanged;
        }

        public void AdjustDrawSizeToDrawSurfaceSize()
        {
            if (_parentForm.WindowState != FormWindowState.Minimized)
            {
                _graphics.PreferredBackBufferWidth = _pictureBox.Width;
                _graphics.PreferredBackBufferHeight = _pictureBox.Height;
                Globals.WindowWidth =
                    Globals.TheCarmera.ViewWidth =
                    Globals.TheMap.ViewWidth =
                    _pictureBox.Width;
                Globals.WindowHeight =
                    Globals.TheCarmera.ViewHeight =
                    Globals.TheMap.ViewHeight =
                    _pictureBox.Height;
                _graphics.ApplyChanges();
            }
        }

        /// <summary>
        /// If game window inactived total times is exceed max interval, pause the game.
        /// </summary>
        /// <param name="gameTime"></param>
        private void UpdateGameActiveState(GameTime gameTime)
        {
            if (!IsActive)
            {
                _totalInactivedSeconds += gameTime.ElapsedGameTime.TotalSeconds;
            }
            else
            {
                _totalInactivedSeconds = 0.0;
            }

            _isInactivedReachMaxInterval = (_totalInactivedSeconds >= MaxInactivedSeconds);
        }

        private void pictureBox_SizeChanged(object sender, EventArgs e)
        {
            AdjustDrawSizeToDrawSurfaceSize();
        }

        private void gameForm_VisibleChanged(object sender, EventArgs e)
        {
            if (_gameForm.Visible)
            {
                _gameForm.Visible = false;

                //Didn't no why, below solved fps slow than normal
                _parentForm.Visible = false;
                _parentForm.WindowState = FormWindowState.Minimized;
                _parentForm.WindowState = FormWindowState.Normal;
                _parentForm.Visible = true;
            }
        }

        private void graphics_PreparingDeviceSettings(object sender, PreparingDeviceSettingsEventArgs e)
        {
            e.GraphicsDeviceInformation.PresentationParameters.
                DeviceWindowHandle = _drawSurface;
        }

        #region Utils
        private void LoadEffect()
        {
            _waterEffect = Content.Load<Effect>(@"effect\refraction");
            _waterfallTexture = Content.Load<Texture2D>(@"effect\waterfall");
        }

        private void WaterEffectBegin()
        {
            GraphicsDevice.SetRenderTarget(_renderTarget);
        }

        private void WaterEffectEnd(GameTime gameTime)
        {
            _spriteBatch.End();
            GraphicsDevice.SetRenderTarget(null);
            // Set an effect parameter to make the
            // displacement texture scroll in a giant circle.
            _waterEffect.Parameters["DisplacementScroll"].SetValue(
                                        MoveInCircle(gameTime, 0.2f));
            // Set the displacement texture.
            _graphics.GraphicsDevice.Textures[1] = _waterfallTexture;

            _spriteBatch.Begin(SpriteSortMode.Deferred,
                null,
                null,
                null,
                null,
                _waterEffect);
            _spriteBatch.Draw(_renderTarget, Vector2.Zero, Color.White);
            _spriteBatch.End();
            _spriteBatch.Begin(SpriteSortMode.Deferred, null);
        }

        static Vector2 MoveInCircle(GameTime gameTime, float speed)
        {
            double time = gameTime.TotalGameTime.TotalSeconds * speed;

            float x = (float)Math.Cos(time);
            float y = (float)Math.Sin(time);

            return new Vector2(x, y);
        }
        #endregion Utils

        /// <summary>
        /// Allows the game to perform any initialization it needs to before starting to run.
        /// This is where it can query for any required services and load any non-graphic
        /// related content.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.
        /// </summary>
        protected override void Initialize()
        {
            Globals.LoadSetting();
            Globals.TheGame = this;
            TalkTextList.Initialize();
            Log.Initialize();

            Log.LogMessageToFile("Game is running...");

            _graphics.IsFullScreen = Globals.IsFullScreen;
            _graphics.PreferredBackBufferWidth = Globals.WindowWidth;
            _graphics.PreferredBackBufferHeight = Globals.WindowHeight;
            _graphics.ApplyChanges();

            //Set back in case of width height not work
            Globals.WindowWidth = _graphics.PreferredBackBufferWidth;
            Globals.WindowHeight = _graphics.PreferredBackBufferHeight;

            Globals.TheCarmera.ViewWidth = _graphics.PreferredBackBufferWidth;
            Globals.TheCarmera.ViewHeight = _graphics.PreferredBackBufferHeight;
            Globals.TheMap.ViewWidth = _graphics.PreferredBackBufferWidth;
            Globals.TheMap.ViewHeight = _graphics.PreferredBackBufferHeight;

            //Game run in editor
            if (_parentForm != null)
            {
                //Make draw size correct
                AdjustDrawSizeToDrawSurfaceSize();
            }

            base.Initialize();
        }

        /// <summary>
        /// LoadContent will be called once per game and is the place to load
        /// all of your content.
        /// </summary>
        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            LoadEffect();
            _renderTarget = new RenderTarget2D(GraphicsDevice,
                Globals.WindowWidth,
                Globals.WindowHeight);
            Globals.FontSize7 = Content.Load<SpriteFont>(@"font\ASCII_Verdana_7_Bold");
            Globals.FontSize10 = Content.Load<SpriteFont>(@"font\GB2312_ASCII_�����ϸԲ_10");
            Globals.FontSize12 = Content.Load<SpriteFont>(@"font\GB2312_ASCII_�����ϸԲ_12");

            //Load partner name list
            PartnerList.Load();

            //Start gui
            GuiManager.Starting();
        }

        /// <summary>
        /// UnloadContent will be called once per game and is the place to unload
        /// all content.
        /// </summary>
        protected override void UnloadContent()
        {
            // TODO: Unload any non ContentManager content here
        }

        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Update(GameTime gameTime)
        {
            UpdateGameActiveState(gameTime);

            if (IsPaused || IsFocusLost)
            {
                base.Update(gameTime);
                SuppressDraw();
                return;
            }

            var mouseState = Mouse.GetState();
            var keyboardState = Keyboard.GetState();

            //Fullscreen toggle
            if (keyboardState.IsKeyDown(Keys.LeftAlt) || keyboardState.IsKeyDown(Keys.RightAlt))
            {
                if (keyboardState.IsKeyDown(Keys.Enter) &&
                    LastKeyboardState.IsKeyUp(Keys.Enter))
                {
                    _graphics.ToggleFullScreen();
                    Globals.IsFullScreen = !Globals.IsFullScreen;
                }
            }

            //Map layer draw toggle
            if (IsInEditMode)
            {
                if (keyboardState.IsKeyDown(Keys.D1) && LastKeyboardState.IsKeyUp(Keys.D1))
                    Globals.TheMap.SwitchLayerDraw(0);
                if (keyboardState.IsKeyDown(Keys.D2) && LastKeyboardState.IsKeyUp(Keys.D2))
                    Globals.TheMap.SwitchLayerDraw(1);
                if (keyboardState.IsKeyDown(Keys.D3) && LastKeyboardState.IsKeyUp(Keys.D3))
                    Globals.TheMap.SwitchLayerDraw(2);
            }


            if (ScriptExecuter.IsInPlayingMovie)
            {
                //Stop movie when Esc key pressed
                if (keyboardState.IsKeyDown(Keys.Escape) &&
                    LastKeyboardState.IsKeyUp(Keys.Escape))
                {
                    ScriptExecuter.StopMovie();
                }
            }
            else
            {
                //Update GUI first, GUI will decide whether user input be intercepted or pass through
                GuiManager.Update(gameTime);

                switch (GameState.State)
                {
                    case GameState.StateType.Start:
                        ScriptManager.RunScript(
                            Utils.GetScriptParser("title.txt"));
                        GameState.State = GameState.StateType.Title;
                        break;
                    case GameState.StateType.Title:
                        break;
                    case GameState.StateType.Playing:
                        if (IsGamePlayPaused) break;

                        if (Globals.IsInSuperMagicMode)
                        {
                            Globals.SuperModeMagicSprite.Update(gameTime);
                            if (Globals.SuperModeMagicSprite.IsDestroyed)
                            {
                                Globals.IsInSuperMagicMode = false;
                                Globals.SuperModeMagicSprite = null;
                            }
                            break;//Just update super magic
                        }
                        //Player
                        Globals.ThePlayer.Update(gameTime);
                        //Magic list
                        MagicManager.Update(gameTime);
                        //Npc list
                        NpcManager.Update(gameTime);
                        //Obj list
                        ObjManager.Update(gameTime);
                        //Map
                        Globals.TheMap.Update(gameTime);
                        //Weather
                        WeatherManager.Update(gameTime);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            //Update script after GuiManager, because script executing rely GUI state.
            ScriptManager.Update(gameTime);

            LastKeyboardState = Keyboard.GetState();
            LastMouseState = mouseState;

            base.Update(gameTime);
        }

        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {
            //Update fps measurer
            Fps.Update(gameTime);

            GraphicsDevice.Clear(BackgroundColor);
            _spriteBatch.Begin(SpriteSortMode.Deferred, null);
            if (ScriptExecuter.IsInPlayingMovie)//Movie
            {
                ScriptExecuter.DrawVideo(_spriteBatch);
            }
            else
            {
                switch (GameState.State)
                {
                    case GameState.StateType.Start:
                        break;
                    case GameState.StateType.Title:
                        break;
                    case GameState.StateType.Playing:
                        DrawGamePlay(gameTime);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                //GUI
                GuiManager.Draw(_spriteBatch);
            }
            _spriteBatch.End();

            base.Draw(gameTime);
        }

        /// <summary>
        /// Draw map npc obj magic etc.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        public void DrawGamePlay(GameTime gameTime)
        {
            if (Globals.IsWaterEffectEnabled)
            {
                WaterEffectBegin();
            }
            //Map npcs objs magic sprite
            Globals.TheMap.Draw(_spriteBatch);
            //Player
            Globals.ThePlayer.Draw(_spriteBatch);
            //Weather
            WeatherManager.Draw(_spriteBatch);
            //Super magic
            if (Globals.IsInSuperMagicMode)
            {
                Globals.SuperModeMagicSprite.Draw(_spriteBatch);
            }
            if (Globals.IsWaterEffectEnabled)
            {
                WaterEffectEnd(gameTime);
            }
            //Fade in, fade out
            if (ScriptExecuter.IsInFadeIn || ScriptExecuter.IsInFadeOut)
            {
                ScriptExecuter.DrawFade(_spriteBatch);
            }
        }

        /// <summary>
        /// Take snapshot.
        /// </summary>
        /// <returns>Texture of snapshot be taken.</returns>
        public Texture2D TakeSnapShot()
        {
            Draw(new GameTime());

            var w = GraphicsDevice.PresentationParameters.BackBufferWidth;
            var h = GraphicsDevice.PresentationParameters.BackBufferHeight;
            var data = new byte[w * h * 4];
            GraphicsDevice.GetBackBufferData(data);
            var texture = new Texture2D(GraphicsDevice, w, h);
            texture.SetData(data);
            return texture;
        }

        /// <summary>
        /// Not safe if player is followed by enemy or existing enemy magic.
        /// </summary>
        /// <returns>True if safe.Otherwise false.</returns>
        public bool IsSafe()
        {
            var npcs = NpcManager.NpcList;
            if (npcs.Any(npc => npc.IsEnemy &&
                npc.IsFollowTargetFound &&
                npc.FollowTarget != null &&
                (npc.FollowTarget.IsPlayer || npc.FollowTarget.IsPartner)))
            {
                return false;
            }
            var magics = MagicManager.MagicSpritesList;
            if (magics.Any(magicSprite => magicSprite.BelongCharacter.IsEnemy))
            {
                return false;
            }
            var workList = MagicManager.WorkList;
            if (workList.Any(workItem => workItem.TheSprite.BelongCharacter.IsEnemy))
            {
                return false;
            }

            return true;
        }

        public void ExitGame()
        {
            Globals.SaveSetting();
            Exit();
        }
    }
}
