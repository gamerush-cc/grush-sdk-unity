namespace GRushSdk.Editor
{
    internal enum GRushGateState
    {
        Checking = 0,
        Allowed,
        Rejected,
        Failed,
    }

    internal sealed class GRushEditorGateStatus
    {
        public GRushGateState State { get; private set; } = GRushGateState.Checking;

        public bool AllowsWrites => State == GRushGateState.Allowed;

        public bool CanRetry => State == GRushGateState.Rejected || State == GRushGateState.Failed;

        public void Begin()
        {
            State = GRushGateState.Checking;
        }

        public bool FailChecking()
        {
            if (State != GRushGateState.Checking)
            {
                return false;
            }
            State = GRushGateState.Failed;
            return true;
        }

        public void Resolve(bool reachable, string minVersion, string version)
        {
            if (!reachable || minVersion == null || Parse(minVersion) == null)
            {
                State = GRushGateState.Failed;
                return;
            }
            State = IsSupported(version, minVersion)
                ? GRushGateState.Allowed
                : GRushGateState.Rejected;
        }

        public static bool IsSupported(string version, string minVersion)
        {
            var current = Parse(version);
            var minimum = Parse(minVersion);
            if (current == null || minimum == null)
            {
                return false;
            }
            for (var index = 0; index < 3; index++)
            {
                if (current[index] != minimum[index])
                {
                    return current[index] > minimum[index];
                }
            }
            return true;
        }

        private static int[] Parse(string value)
        {
            var parts = (value ?? "").Split('.');
            if (parts.Length != 3)
            {
                return null;
            }
            var numbers = new int[3];
            for (var index = 0; index < 3; index++)
            {
                if (!IsDigits(parts[index]) || !int.TryParse(parts[index], out numbers[index]))
                {
                    return null;
                }
            }
            return numbers;
        }

        private static bool IsDigits(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }
            foreach (var c in value)
            {
                if (c < '0' || c > '9')
                {
                    return false;
                }
            }
            return true;
        }
    }
}
