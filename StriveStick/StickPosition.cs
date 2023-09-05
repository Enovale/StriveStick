using System.Numerics;

namespace StriveStick
{
    public class StickPosition
    {
        private readonly sbyte _valueX = 0;
        private readonly sbyte _valueY = 0;

        public StickPosition(sbyte x, sbyte y)
        {
            _valueX = x;
            _valueY = y;
        }

        public StickPosition(int x, int y)
        {
            _valueX = (sbyte)x;
            _valueY = (sbyte)y;
        }

        public override bool Equals(object? obj) => Equals(obj as StickPosition);

        public bool Equals(StickPosition? other)
        {
            if (other is null)
                return false;

            if (ReferenceEquals(this, other))
                return true;

            if (GetType() != other.GetType())
                return false;

            return _valueX == other._valueX && _valueY == other._valueY;
        }
        
        public static bool operator ==(StickPosition? lhs, StickPosition? rhs)
        {
            if (lhs is null)
            {
                if (rhs is null)
                    return true;

                // Only the left side is null.
                return false;
            }
            
            // Equals handles case of null on right side.
            return lhs.Equals(rhs);
        }

        public static bool operator !=(StickPosition? lhs, StickPosition? rhs) => !(lhs == rhs);

        public static implicit operator StickPosition(Vector2 other) => new((sbyte)other.X, (sbyte)other.Y);
        public static implicit operator Vector2(StickPosition other) => new(other._valueX, other._valueY);

        public override string ToString()
        {
            return $"({_valueX}, {_valueY})";
        }
    }
}