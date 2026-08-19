using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace SlipManagement2
{
    // Identifies the computer the program is installed on, for the single-machine licence.
    //
    // Three independent sources are read rather than one, and each is hashed separately, so a
    // licence can survive ordinary hardware maintenance. Binding to a single value would mean a
    // replaced disk or a reinstalled Windows locks the operator out of their own records on a
    // Monday morning, and that call costs more than the copying it prevents.
    public static class MachineFingerprint
    {
        // A component that could not be read at all. Distinguished from a component that read a
        // DIFFERENT value: an absent source is treated as neutral rather than as a mismatch, so a
        // machine with WMI disabled or a blank OEM serial is not locked out by its own hardware.
        public const string Unavailable = "";

        private static string[] _cached;

        // [0] Windows installation GUID  [1] motherboard serial  [2] processor ID
        // Order is fixed: it is part of the wire format of the machine code.
        public static string[] Components()
        {
            if (_cached != null) return _cached;

            _cached = new[]
            {
                Hash(ReadRegistry(@"SOFTWARE\Microsoft\Cryptography", "MachineGuid")),
                Hash(ReadWmi("Win32_BaseBoard", "SerialNumber")),
                Hash(ReadWmi("Win32_Processor", "ProcessorId")),
            };
            return _cached;
        }

        // The code the operator reads out or emails. Six groups of five hex characters, covering
        // all three components so the licence can be checked against each one independently.
        // Hex rather than base32: nothing in it can be confused over a telephone.
        public static string MachineCode()
        {
            var sb = new StringBuilder();
            foreach (string c in Components())
                sb.Append(string.IsNullOrEmpty(c) ? new string('0', CodeChars) : Prefix(c));

            string raw = sb.ToString().ToUpperInvariant();
            var grouped = new StringBuilder();
            for (int i = 0; i < raw.Length; i += 5)
            {
                if (grouped.Length > 0) grouped.Append('-');
                grouped.Append(raw.Substring(i, Math.Min(5, raw.Length - i)));
            }
            return grouped.ToString();
        }

        // Splits a machine code back into its three component hashes. Returns null if the text is
        // not a machine code at all, so a mistyped licence is refused rather than half-read.
        public static string[] ParseMachineCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return null;

            string raw = code.Replace("-", "").Replace(" ", "").Trim().ToLowerInvariant();
            if (raw.Length != 30) return null;
            foreach (char ch in raw)
                if (!Uri.IsHexDigit(ch)) return null;

            return new[] { raw.Substring(0, 10), raw.Substring(10, 10), raw.Substring(20, 10) };
        }

        // How many components this machine shares with the licensed one, and how many could be
        // compared at all. The caller decides what is enough -- see Licence.IsThisMachine.
        public static void Compare(string[] licensed, out int matches, out int comparable)
        {
            matches = 0; comparable = 0;
            if (licensed == null) return;

            string[] here = Components();
            for (int i = 0; i < 3 && i < licensed.Length; i++)
            {
                // Components() holds the FULL SHA-256; a machine code carries only the first ten
                // characters of each, which is all ParseMachineCode can give back. Compare the
                // same ten either side. Comparing the whole hash against the prefix silently
                // failed for every machine, including the one the licence was issued for.
                string mine   = Prefix(here[i]);
                string theirs = Prefix(licensed[i]);

                if (IsBlank(mine) || IsBlank(theirs)) continue;   // neutral, not a mismatch

                comparable++;
                if (string.Equals(mine, theirs, StringComparison.OrdinalIgnoreCase)) matches++;
            }
        }

        private const int CodeChars = 10;

        private static string Prefix(string hash)
        {
            if (string.IsNullOrEmpty(hash)) return "";
            return hash.Length <= CodeChars ? hash : hash.Substring(0, CodeChars);
        }

        private static bool IsBlank(string part)
        {
            return string.IsNullOrEmpty(part) || part == new string('0', CodeChars);
        }

        // ===================================================================

        private static string Hash(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return Unavailable;
            using (var sha = SHA256.Create())
            {
                byte[] h = sha.ComputeHash(Encoding.UTF8.GetBytes(value.Trim().ToUpperInvariant()));
                var sb = new StringBuilder();
                foreach (byte b in h) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        private static string ReadRegistry(string keyPath, string valueName)
        {
            try
            {
                // 64-bit view explicitly: under WOW64 the default view would hand back a different
                // MachineGuid than a 64-bit build sees, so the same PC would look like two.
                using (var baseKey = Microsoft.Win32.RegistryKey.OpenBaseKey(
                           Microsoft.Win32.RegistryHive.LocalMachine,
                           Microsoft.Win32.RegistryView.Registry64))
                using (var key = baseKey.OpenSubKey(keyPath))
                {
                    return key?.GetValue(valueName)?.ToString();
                }
            }
            catch { return null; }
        }

        private static string ReadWmi(string wmiClass, string property)
        {
            try
            {
                using (var searcher = new System.Management.ManagementObjectSearcher(
                           "SELECT " + property + " FROM " + wmiClass))
                using (var results = searcher.Get())
                {
                    foreach (System.Management.ManagementObject mo in results)
                    {
                        string v = mo[property]?.ToString();
                        mo.Dispose();
                        // Some OEM boards report a placeholder rather than leaving it blank.
                        // Treat those as unreadable, or every machine of that model matches.
                        if (string.IsNullOrWhiteSpace(v)) continue;
                        string t = v.Trim();
                        if (t.Equals("To be filled by O.E.M.", StringComparison.OrdinalIgnoreCase) ||
                            t.Equals("Default string", StringComparison.OrdinalIgnoreCase) ||
                            t.Equals("None", StringComparison.OrdinalIgnoreCase) ||
                            t.Equals("System Serial Number", StringComparison.OrdinalIgnoreCase) ||
                            t.All(c => c == '0')) continue;
                        return t;
                    }
                }
            }
            catch { }
            return null;
        }
    }
}
