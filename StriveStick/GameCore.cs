using System.Drawing;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using Chroma;
using Chroma.ContentManagement;
using Chroma.Diagnostics;
using Chroma.Graphics;
using Chroma.Graphics.Batching;
using Chroma.Graphics.TextRendering.TrueType;
using Chroma.Input;
using Chroma.Input.GameControllers;
using Color = Chroma.Graphics.Color;

namespace StriveStick
{
    public class GameCore : Game
    {
        private static readonly Dictionary<GAME_ACTION, ScanCode> _keyMap = new()
        {
            { GAME_ACTION.UP, ScanCode.W },
            { GAME_ACTION.RIGHT, ScanCode.D },
            { GAME_ACTION.DOWN, ScanCode.S },
            { GAME_ACTION.LEFT, ScanCode.A },
            { GAME_ACTION.PUNCH, ScanCode.U },
            { GAME_ACTION.KICK, ScanCode.J },
            { GAME_ACTION.SLASH, ScanCode.I },
            { GAME_ACTION.HEAVYSLASH, ScanCode.K },
            { GAME_ACTION.DUST, ScanCode.O },
            { GAME_ACTION.ROMANCANCEL, ScanCode.L },
            { GAME_ACTION.DASH, ScanCode.Space },
            { GAME_ACTION.TAUNT, ScanCode.B },
            { GAME_ACTION.FAULTLESSDEFENSE, ScanCode.M },
        };
        
        private static readonly Dictionary<GAME_ACTION, ControllerButton> _gamePadMap = new()
        {
            { GAME_ACTION.UP, ControllerButton.DpadUp },
            { GAME_ACTION.RIGHT, ControllerButton.DpadRight },
            { GAME_ACTION.DOWN, ControllerButton.DpadDown },
            { GAME_ACTION.LEFT, ControllerButton.DpadLeft },
            { GAME_ACTION.PUNCH, ControllerButton.X },
            { GAME_ACTION.KICK, ControllerButton.A },
            { GAME_ACTION.SLASH, ControllerButton.Y },
            { GAME_ACTION.HEAVYSLASH, ControllerButton.B },
            { GAME_ACTION.DUST, ControllerButton.RightBumper },
            { GAME_ACTION.ROMANCANCEL, ControllerButton.RightBottomPaddle },
            { GAME_ACTION.DASH, ControllerButton.LeftStick },
            { GAME_ACTION.FAULTLESSDEFENSE, ControllerButton.RightStick },
        };
        
        private static readonly Dictionary<GAME_ACTION, ControllerAxis> _triggerMap = new()
        {
            { GAME_ACTION.ROMANCANCEL, ControllerAxis.RightTrigger },
        };

        private static readonly Dictionary<GAME_ACTION, string> _buttonMap = new()
        {
            { GAME_ACTION.PUNCH, "P" },
            { GAME_ACTION.KICK, "K" },
            { GAME_ACTION.SLASH, "S" },
            { GAME_ACTION.HEAVYSLASH, "HS" },
            { GAME_ACTION.DUST, "D" },
            { GAME_ACTION.ROMANCANCEL, "RC" },
            { GAME_ACTION.DASH, "DA" },
            { GAME_ACTION.TAUNT, "T" },
            { GAME_ACTION.FAULTLESSDEFENSE, "FD" },
            /*
            { GAME_ACTION.DASH, "\ud83c\udfc3" },
            { GAME_ACTION.TAUNT, "\ud83d\ude1b" },
            { GAME_ACTION.FAULTLESSDEFENSE, "\ud83d\udee1\ufe0f" },
            */
        };

        private static readonly Dictionary<GAME_ACTION, Color> _colorMap = new()
        {
            { GAME_ACTION.PUNCH, Color.Pink },
            { GAME_ACTION.KICK, Color.Blue },
            { GAME_ACTION.SLASH, Color.LimeGreen },
            { GAME_ACTION.HEAVYSLASH, Color.Red },
            { GAME_ACTION.DUST, Color.Orange },
            { GAME_ACTION.ROMANCANCEL, Color.DarkMagenta },
            { GAME_ACTION.DASH, Color.White },
            { GAME_ACTION.TAUNT, Color.Yellow },
            { GAME_ACTION.FAULTLESSDEFENSE, Color.LimeGreen },
        };

        private static readonly float _roundRatio = 0.85f;

        private static readonly Color _bgColor = new(0, 255, 0);
        private static readonly Color _stickColor = new(255, 0, 0);
        private static readonly Color _buttonColor = new(107, 107, 107);
        private static readonly Color _boardBgColor = new(153, 153, 153);
        private static readonly Color _boardAccentColor = new(242, 242, 242);

        private static readonly Color[] _historyLineGradient = new[]
        {
            new Color(255, 0, 0),
            new Color(0, 0, 255),
            new Color(255, 0, 0)
        };

        private static readonly float _historyLineTime = 0.5f;

        private static readonly float _buttonLifetime = .8f;

        private StickPosition _inputPosition = Vector2.Zero;

        private Stack<(StickPosition position, float timeSinceLastInput)> _positions = new(50);

        private Queue<PressedButton> _buttonPresses = new();

        private float _timeSinceLastInput = 0;

        private List<GAME_ACTION> _pressedActions = new();

        private TrueTypeFont _font = null!;

        private bool _renderButtons = true;

        private bool _renderHistoryLine = true;

        private bool _renderStick = true;

        public GameCore() : base(new(false, false))
        {
            FixedTimeStepTarget = 60;
            Window.Size = new(140, 140);
            Window.CanResize = true;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                WindowsParts.MakeWindowTransparent(Window);
            var sdl2Type = Type.GetType("Chroma.Natives.Bindings.SDL.SDL2, Chroma.Natives, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
            var setHint = sdl2Type?.GetMethod("SDL_SetHint", BindingFlags.Static | BindingFlags.Public);
            setHint?.Invoke(null, ["SDL_JOYSTICK_ALLOW_BACKGROUND_EVENTS", "1"]);
        }

        protected override void Initialize(IContentProvider content)
        {
            _font = TrueTypeFont.Default;
            _font.Height = 24;
            //_font = Content.Load<TrueTypeFont>("NotoColorEmoji.ttf");
        }

        protected override void KeyPressed(KeyEventArgs e)
        {
            if (e.ScanCode == ScanCode.F1)
            {
                FixedTimeStepTarget = 60;
            }
            else if (e.ScanCode == ScanCode.F2)
            {
                FixedTimeStepTarget = 185;
            }
            else if (e.ScanCode == ScanCode.F3)
            {
                _renderButtons = !_renderButtons;
            }
            else if (e.ScanCode == ScanCode.F4)
            {
                _renderHistoryLine = !_renderHistoryLine;
            }
            else if (e.ScanCode == ScanCode.F5)
            {
                _renderStick = !_renderStick;
            }
        }

        protected override void MouseMoved(MouseMoveEventArgs e)
        {
            if (e.ButtonState.Left)
            {
                Window.Position += e.Position;
            }
        }

        protected override void FixedUpdate(float delta)
        {
            Window.Title = PerformanceCounter.FPS.ToString();
            if (_timeSinceLastInput < 1)
                _timeSinceLastInput += delta;

            var amountToRemove = 0;
            foreach (var buttonPress in _buttonPresses)
            {
                buttonPress.RemainingLifetime -= delta;

                if (buttonPress.RemainingLifetime < 0)
                    amountToRemove++;
            }

            for (int i = 0; i < amountToRemove; i++)
            {
                _buttonPresses.Dequeue();
            }

            // Get stick position
            // This assumes we are using a keyboard and does not currently support any analog movement
            // But I think it would be fairly easy to add it in an existing system
            var x = (Keyboard.IsKeyDown(_keyMap[GAME_ACTION.RIGHT]) ? 1 : 0) +
                    (Keyboard.IsKeyDown(_keyMap[GAME_ACTION.LEFT]) ? -1 : 0);
            var y = (Keyboard.IsKeyDown(_keyMap[GAME_ACTION.DOWN]) ? 1 : 0) +
                    (Keyboard.IsKeyDown(_keyMap[GAME_ACTION.UP]) ? -1 : 0);
            
            if (Controller.DeviceCount > 0)
            {
                var nX = (Controller.IsButtonDown(0, _gamePadMap[GAME_ACTION.RIGHT]) ? 1 : 0) +
                        (Controller.IsButtonDown(0, _gamePadMap[GAME_ACTION.LEFT]) ? -1 : 0);
                var nY = (Controller.IsButtonDown(0, _gamePadMap[GAME_ACTION.DOWN]) ? 1 : 0) +
                        (Controller.IsButtonDown(0, _gamePadMap[GAME_ACTION.UP]) ? -1 : 0);
                var aX = Controller.GetAxisValueNormalized(0, ControllerAxis.LeftStickX);
                var aY = Controller.GetAxisValueNormalized(0, ControllerAxis.LeftStickY);

                if (nX != 0 || nY != 0)
                {
                    x = nX;
                    y = nY;
                }
                else if ((aX > 0.5f || aX < -0.5f) || (aY > 0.5f || aY < -0.5f))
                {
                    x = (int)MathF.Round(aX);
                    y = (int)MathF.Round(aY);
                }
            }
            
            var currentPos = new StickPosition(x, y);

            // If we are holding a different position than before, push that old one on
            // to the stack and add the current position to the buffer and reset the timer.
            if (currentPos != _inputPosition)
            {
                _positions.Push(new(_inputPosition, _timeSinceLastInput));
                _inputPosition = currentPos;
                _timeSinceLastInput = 0;
                Console.WriteLine(_inputPosition);
            }

            // This code SUCKS.
            var cPresses = new List<GAME_ACTION>(_pressedActions);
            if (Controller.DeviceCount > 0)
            {
                foreach (var kvp in _gamePadMap)
                {
                    if (_buttonMap.ContainsKey(kvp.Key))
                    {
                        if (Controller.IsButtonDown(0, kvp.Value))
                        {
                            if (!_pressedActions.Contains(kvp.Key))
                            {
                                cPresses.Add(kvp.Key);
                                _buttonPresses.Enqueue(new(kvp.Key, currentPos, _buttonLifetime));
                            }
                        }
                        else
                        {
                            cPresses.Remove(kvp.Key);
                        }
                    }
                }
                
                foreach (var kvp in _triggerMap)
                {
                    if (_buttonMap.ContainsKey(kvp.Key))
                    {
                        if (Controller.GetAxisValueNormalized(0, kvp.Value) >= 0.9f)
                        {
                            if (!_pressedActions.Contains(kvp.Key))
                            {
                                cPresses.Add(kvp.Key);
                                _buttonPresses.Enqueue(new(kvp.Key, currentPos, _buttonLifetime));
                            }
                        }
                        else
                        {
                            cPresses.Remove(kvp.Key);
                        }
                    }
                }
            }

            foreach (var kvp in _keyMap)
            {
                if (_buttonMap.ContainsKey(kvp.Key))
                {
                    if (Keyboard.IsKeyDown(kvp.Value))
                    {
                        if (!_pressedActions.Contains(kvp.Key))
                        {
                            _pressedActions.Add(kvp.Key);
                            _buttonPresses.Enqueue(new(kvp.Key, currentPos, _buttonLifetime));
                        }
                    }
                    else if (!cPresses.Contains(kvp.Key))
                    {
                        _pressedActions.Remove(kvp.Key);
                    }
                }
            }
            
            foreach (var gameAction in cPresses)
            {
                if (!_pressedActions.Contains(gameAction))
                    _pressedActions.Add(gameAction);
            }
        }

        protected override void Draw(RenderContext context)
        {
            context.Clear(_bgColor);
            DrawInputDisplay(context, new(20, 20), new(100, 100));
            //TestGradient(context);
        }

        private void DrawInputDisplay(RenderContext context, Vector2 pos, Size size)
        {
            // Offset the whole board a bit so it fits within the bounds of the size set
            // This is honestly redundant I don't think it really matters but yknow
            var offset = new Vector2(2, 2);
            var realSize = size - (new Size((int)offset.X, (int)offset.Y) * 2);
            var realPos = pos + offset;
            var quadrantSize = size.Width / 2f;
            var ratioOffset = quadrantSize * (1 - _roundRatio);
            
            // Calculate a map of all the possible stick positions and where they are in screenspace
            // This calculation is pretty horrible and could easily be cached, especially in a ECS
            // Note that this is used to draw a polygon so the position order is important.
            // It goes from top left, wrapping around in a circle clockwise, and then we append the center point.
            // The center is chopped off before drawing the polygon. I hate this process but it was 3 AM alright.
            var vertexMap = new Dictionary<Vector2, Vector2>()
            {
                { new(-1, -1), realPos + new Vector2(ratioOffset) },
                { new(0, -1), realPos + new Vector2(quadrantSize, 0) },
                { new(1, -1), realPos + new Vector2(realSize.Width, 0) - new Vector2(ratioOffset, -ratioOffset) },
                { new(1, 0), realPos + new Vector2(realSize.Width, quadrantSize) },
                { new(1, 1), realPos + new Vector2(realSize.Width, realSize.Height) - new Vector2(ratioOffset) },
                { new(0, 1), realPos + new Vector2(quadrantSize, realSize.Height) },
                { new(-1, 1), realPos + new Vector2(0, realSize.Height) + new Vector2(ratioOffset, -ratioOffset) },
                { new(-1, 0), realPos + new Vector2(0, quadrantSize) },
                { new(0, 0), realPos + new Vector2(quadrantSize) },
            };
            DrawBoard(context, vertexMap.Values.ToList(), 4);

            if (_positions.Count > 0)
            {
                if (_renderHistoryLine)
                {
                    // Draw our history line first
                    DrawHistoryLine(context, vertexMap, 5);
                }

                if (_renderStick)
                {
                    // Draw a faded stick that tweens between the old stick position and the new one,
                    // instantly snapping to the current if it is interrupted.
                    var oldPos = vertexMap[_positions.Peek().position];
                    var dir = vertexMap[_inputPosition] - oldPos;
                    var tweenPoint = (oldPos + (EaseOut(_timeSinceLastInput) * dir));
                    DrawStick(context, tweenPoint, 10, 0.5f);
                }
            }

            if (_renderStick)
                DrawStick(context, vertexMap[_inputPosition], 10, 1);

            if (_renderButtons)
            {
                // Now we draw the buttons the user has recently pressed.
                foreach (var buttonPress in _buttonPresses.Reverse())
                {
                    var t = 1 - (buttonPress.RemainingLifetime / _buttonLifetime);
                    var et = EaseOut(t, 5);
                    var ret = 1 - et;
                    // This is not correct, in game the button fully covers things below for most of it's lifetime
                    // I'm sick of thinking about it.
                    var alphaT = 1 - EaseIn(et, 5);
                    var r = Interpolate(et, 18, 6);

                    var buttonPos = vertexMap[buttonPress.StickPosition];
                    context.Circle(ShapeMode.Fill, buttonPos, r, _buttonColor.Alpha(alphaT));
                    var str = _buttonMap[buttonPress.Action];
                    var measure = _font.Measure(str) + new Size((int)ret, (int)ret);
                    var halfMeasure = measure / 2;
                    context.DrawString(str, buttonPos - new Vector2(halfMeasure.Width, halfMeasure.Height),
                        _colorMap[buttonPress.Action].Alpha(alphaT));
                }
            }
        }

        private void DrawBoard(RenderContext context, List<Vector2> vertices, float radius)
        {
            RenderSettings.LineThickness = 3;
            // We dont want the center point for drawing the poly
            var polyVerts = vertices.Take(8).ToList();
            context.Polygon(ShapeMode.Fill, polyVerts, _boardBgColor);
            context.Polygon(ShapeMode.Stroke, polyVerts, _boardAccentColor);

            foreach (var vertex in vertices)
            {
                context.Circle(ShapeMode.Fill, vertex, radius, _boardAccentColor);
            }
        }

        private void DrawStick(RenderContext context, Vector2 pos, float radius, float opacity)
        {
            context.Circle(ShapeMode.Fill, pos, radius, _stickColor.Alpha(opacity));
        }

        // Definitely the most complicated code here.
        // Basically we traverse through the history of inputs forward-to-back in time,
        // draw a line between each input and the next, along with a circle at each point.
        // Then once the specified amount of history time has elapsed we shrink the line quick and stop.
        // NOTE: The shrinking should only shrink the last segment but it currently just guesses based on a small
        // time offset because all other solutions i've found cause other issues and I was really tired.
        // OTHER NOTE: The draw calls are batched and drawn in reverse so that the shrunk part of the line
        // is always covered up by what comes after because of the way we're iterating the inputs.
        private void DrawHistoryLine(RenderContext context, Dictionary<Vector2, Vector2> vertexMap, float radius)
        {
            var totalTime = 0f;
            var posArray = _positions.ToArray();
            var small = false;
            for (var i = -1; i < posArray.Length - 1; i++)
            {
                var next = posArray[i + 1];
                var current = i == -1 ? new(_inputPosition, _timeSinceLastInput) : posArray[i];
                
                totalTime += current.timeSinceLastInput;

                if (totalTime > _historyLineTime)
                    break;
                
                if (totalTime > _historyLineTime - 0.05f)
                {
                    small = true;
                }
                
                var gradient = GetGradient(totalTime / _historyLineTime, _historyLineGradient);
                var realRadius = radius;
                var isSmall = small;
                context.Batch(r =>
                {
                    if (isSmall)
                    {
                        RenderSettings.LineThickness = 2;
                        realRadius /= 2;
                    }
                    else
                    {
                        RenderSettings.LineThickness = 6;
                        realRadius = radius;
                    }
                    r.Line(vertexMap[current.position], vertexMap[next.position], gradient);
                    r.Circle(ShapeMode.Fill, vertexMap[next.position], realRadius, gradient);
                }, i);
            }
            context.DrawBatch(DrawOrder.FrontToBack);
        }

        // Draw a simple segment gradient to the screen to make sure my gradient code is right
        private void TestGradient(RenderContext context)
        {
            var offset = new Vector2(100, 250);
            var segmentSize = 20;
            var segments = 30;
            for (int i = 0; i < segments; i++)
            {
                context.Line(offset with { X = offset.X + (i * segmentSize) }, offset with { X = offset.X + ((i + 1) * segmentSize) }, GetGradient((float)i / segments, _historyLineGradient));
            }
        }

        // Simply interpolates between two colors based on t.
        private Color GetGradient(float t, Color colorA, Color colorB)
        {
            var red = Interpolate(t, colorA.R, colorB.R);
            var green = Interpolate(t, colorA.G, colorB.G);
            var blue = Interpolate(t, colorA.B, colorB.B);
            var alpha = Interpolate(t, colorA.A, colorB.A);
            return new Color((byte)red, (byte)green, (byte)blue, (byte)alpha);
        }

        private float Interpolate(float t, float a, float b)
        {
            return a + t * (b - a);
        }

        // Interpolates between an arbitrary set of colors based on t
        // All Colors are evenly spaced
        private Color GetGradient(float t, params Color[] gradient)
        {
            if (t == 0)
                return gradient[0];
            else if (t == 1)
                return gradient[^1];
            
            var numColors = gradient.Length - 1;
            var space = 1f / numColors;
            var closest = (int)MathF.Floor(t / space);
            var scaledTime = (t - (closest * space)) / space;
            return GetGradient(scaledTime, gradient[closest], gradient[closest + 1]);
        }

        // EaseOutQuint
        private float EaseOut(float number, int factor = 30)
        {
            return 1 - MathF.Pow(1 - number, factor);
        }

        // EaseInQuint
        private float EaseIn(float number, int factor = 30)
        {
            return MathF.Pow(number, factor);
        }
    }
}