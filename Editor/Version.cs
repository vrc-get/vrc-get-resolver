using System;

namespace Anatawa12.VrcGetResolver
{
    readonly struct Version: IComparable<Version>
    {
        public readonly int Major;
        public readonly int Minor;
        public readonly int Patch;
        public readonly string Prerelease;

        public Version(string v)
        {
            var hyphen = v.Split(new[] {'-'}, 2);
            var prerelease = v.Length == 1 ? null : v.Split(new[] {'+'}, 2)[0];
            var version = hyphen[0].Split('.');
            Major = int.Parse(version[0]);
            Minor = int.Parse(version[1]);
            Patch = int.Parse(version[2]);
            Prerelease = prerelease;
        }

        public Version(int major, int minor, int patch) : this()
        {
            Major = major;
            Minor = minor;
            Patch = patch;
        }

        public bool Equals(Version other)
        {
            return Major == other.Major && Minor == other.Minor && Patch == other.Patch && Prerelease == other.Prerelease;
        }

        public override bool Equals(object obj)
        {
            return obj is Version other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Major;
                hashCode = (hashCode * 397) ^ Minor;
                hashCode = (hashCode * 397) ^ Patch;
                hashCode = (hashCode * 397) ^ (Prerelease != null ? Prerelease.GetHashCode() : 0);
                return hashCode;
            }
        }

        public override string ToString() =>
            Prerelease == null ? $"{Major}.{Minor}.{Patch}" : $"{Major}.{Minor}.{Patch}-{Prerelease}";

        public static bool operator ==(Version a, Version b) => a.Equals(b);
        public static bool operator !=(Version a, Version b) => !(a == b);

        public static bool operator <(Version a, Version b) => a.CompareTo(b) < 0;
        public static bool operator >(Version a, Version b) => a.CompareTo(b) > 0;
        public static bool operator <=(Version a, Version b) => a.CompareTo(b) <= 0;
        public static bool operator >=(Version a, Version b) => a.CompareTo(b) >= 0;

        public int CompareTo(Version other)
        {
            var majorComparison = Major.CompareTo(other.Major);
            if (majorComparison != 0) return majorComparison;
            var minorComparison = Minor.CompareTo(other.Minor);
            if (minorComparison != 0) return minorComparison;
            var patchComparison = Patch.CompareTo(other.Patch);
            if (patchComparison != 0) return patchComparison;
            return string.Compare(Prerelease, other.Prerelease, StringComparison.Ordinal);
        }
    }
}