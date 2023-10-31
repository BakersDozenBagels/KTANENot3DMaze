using System;

namespace ThreeDMaze
{
    struct Coordinate : IEquatable<Coordinate>
    {
        public int X { get; private set; }
        public int Y { get; private set; }

        public Coordinate(int x, int y) { X = x; Y = y; }

        public bool Equals(Coordinate other) => other.X == X && other.Y == Y;

        public override int GetHashCode() => unchecked(X * 37 + Y);

        public override bool Equals(object obj) => (obj is Coordinate) && Equals((Coordinate)obj);

        public static bool operator ==(Coordinate a, Coordinate b) => a.Equals(b);
        public static bool operator !=(Coordinate a, Coordinate b) => !(a == b);

        public override string ToString() => "(" + (X + 1) + "," + (Y + 1) + ")";
    }
}
