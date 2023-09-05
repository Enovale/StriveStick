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
        private static readonly Color _lineGradientA = new(255, 0, 0);
        private static readonly Color _lineGradientB = new(0, 0, 255);
        private static readonly Color _lineGradientC = _lineGradientA;

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

            var x = (Keyboard.IsKeyDown(_keyMap[GAME_ACTION.RIGHT]) ? 1 : 0) +
                    (Keyboard.IsKeyDown(_keyMap[GAME_ACTION.LEFT]) ? -1 : 0);
            var y = (Keyboard.IsKeyDown(_keyMap[GAME_ACTION.DOWN]) ? 1 : 0) +
                    (Keyboard.IsKeyDown(_keyMap[GAME_ACTION.UP]) ? -1 : 0);
            var currentPos = new StickPosition(x, y);

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
        }

        private void DrawInputDisplay(RenderContext context, Vector2 pos, Size size)
        {
            var offset = new Vector2(2, 2);
            var realSize = size - (new Size((int)offset.X, (int)offset.Y) * 2);
            var realPos = pos + offset;
            var quadrantSize = size.Width / 2f;
            var ratioOffset = quadrantSize * (1 - _roundRatio);
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
                DrawHistoryLine(context, vertexMap, 5);
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
                
                var gradient = GetGradient(totalTime / _historyLineTime, _lineGradientA, _lineGradientB,
                    _lineGradientC);
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

        private Color GetGradient(float t, Color a, Color b, Color c)
        {
            var interpColor = t < 0.5f ? a : c;
            var red = b.R + t * (interpColor.R - b.R);
            var green = b.G + t * (interpColor.G - b.G);
            var blue = b.B + t * (interpColor.B - b.B);
            var alpha = b.A + t * (interpColor.A - b.A);
            return new Color((byte)red, (byte)green, (byte)blue, (byte)alpha);
        }

        // EaseOutElastic
        private float Ease(float number)
        {
            return 1 - MathF.Pow(1 - number, 30);
        }
    }
}