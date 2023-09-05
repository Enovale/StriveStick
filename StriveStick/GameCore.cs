using System.Drawing;
using System.Numerics;
using Chroma;
using Chroma.Diagnostics;
using Chroma.Graphics;
using Chroma.Graphics.Batching;
using Chroma.Input;
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
        };

        private static readonly float _roundRatio = 0.85f;

        private static readonly Color _stickColor = new(255, 0, 0);
        private static readonly Color _boardBgColor = new(153, 153, 153);
        private static readonly Color _boardAccentColor = new(242, 242, 242);

        private static readonly Color[] _historyLineGradient = new[]
        {
            new Color(255, 0, 0),
            new Color(0, 0, 255),
            new Color(255, 0, 0)
        };

        private static readonly float _historyLineTime = 0.5f;

        private StickPosition _inputPosition = Vector2.Zero;

        private Stack<(StickPosition position, float timeSinceLastInput)> _positions = new(50);

        private float _timeSinceLastInput = 0;

        public GameCore() : base(new(false, false))
        {
            FixedTimeStepTarget = 60;
        }

        protected override void FixedUpdate(float delta)
        {
            Window.Title = PerformanceCounter.FPS.ToString();
            if (_timeSinceLastInput < 1)
                _timeSinceLastInput += delta;

            // Get stick position
            // This assumes we are using a keyboard and does not currently support any analog movement
            // But I think it would be fairly easy to add it in an existing system
            var x = (Keyboard.IsKeyDown(_keyMap[GAME_ACTION.RIGHT]) ? 1 : 0) +
                    (Keyboard.IsKeyDown(_keyMap[GAME_ACTION.LEFT]) ? -1 : 0);
            var y = (Keyboard.IsKeyDown(_keyMap[GAME_ACTION.DOWN]) ? 1 : 0) +
                    (Keyboard.IsKeyDown(_keyMap[GAME_ACTION.UP]) ? -1 : 0);
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
        }

        protected override void Draw(RenderContext context)
        {
            DrawInputDisplay(context, new(100, 100), new(100, 100));
            TestGradient(context);
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
                // Draw our history line first
                DrawHistoryLine(context, vertexMap, 5);
                
                // Draw a faded stick that tweens between the old stick position and the new one,
                // instantly snapping to the current if it is interrupted.
                var oldPos = vertexMap[_positions.Peek().position];
                var dir = vertexMap[_inputPosition] - oldPos;
                var tweenPoint = (oldPos + (Ease(_timeSinceLastInput) * dir));
                DrawStick(context, tweenPoint, 10, 0.5f);
            }

            DrawStick(context, vertexMap[_inputPosition], 10, 1);
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
            var red = colorA.R + t * (colorB.R - colorA.R);
            var green = colorA.G + t * (colorB.G - colorA.G);
            var blue = colorA.B + t * (colorB.B - colorA.B);
            var alpha = colorA.A + t * (colorB.A - colorA.A);
            return new Color((byte)red, (byte)green, (byte)blue, (byte)alpha);
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
        private float Ease(float number)
        {
            return 1 - MathF.Pow(1 - number, 30);
        }
    }
}