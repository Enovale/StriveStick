using System.Numerics;

namespace StriveStick
{
    public class PressedButton
    {
        public GAME_ACTION Action;
        public Vector2 StickPosition;
        public float RemainingLifetime;

        public PressedButton(GAME_ACTION action, Vector2 stickPosition, float remainingLifetime)
        {
            Action = action;
            StickPosition = stickPosition;
            RemainingLifetime = remainingLifetime;
        }
    }
}