using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SlipManagement2
{
    // Single-machine licensing.
    //
    // The deliberate shape of this: it decides whether NEW records may be created, and never
    // whether existing ones may be read. An operator whose licence stops matching can still open
    // Slip History, search it, and export it to Excel. Their records are their property, and the
    // LICENCE.txt shipped with the program says so; a lock that could hold a quarry's own
    // weighbridge records hostage would be a worse thing than the copying it exists to discourage.
    public static class Licence
    {
        public enum State
        {
            Valid,          // signed by the author and issued to this machine
            Missing,        // no licence file present
            Unreadable,     // present but not a licence, or the signature does not verify
            OtherMachine,   // genuine and signed, but issued to a different computer
        }

        // Only the public half ships. A licence cannot be produced without the private key, which
        // never leaves the author's machine, so a forged licence is not something an operator can
        // stumble into -- they would have to break RSA rather than edit a text file.
        private const string PublicKeyXml =
            "<RSAKeyValue><Modulus>y2xnN0fzOu/2LLB29eHMUX9IAVlRM+NId7N6LNlaaNjeP+bQCzh/krCjIcFmWcNt" +
            "q51Uzabm6dUGsc205kocNX9nCH2ERYZn4XKWF706uSOzRn1IFV9AEBeO8srLyfis65UFVwzGABUG02ZL9hODb9ey" +
            "VwIc5JteGyncnCYc21dSCX/yzDDp3WxyDJ+Mqv9ZHGb+D6WxfLCc3sCzRT6+VYlWIjWs2TIJd5yhmW3A7eeeiFA5" +
            "/NQSHZ7Z2LhAoARq6mCZDMU0YKeTRYaqXfazVKBehFrHKlfaXcxN10CTozf9DVLjV+nCtCzwscwP3ZwxXMaLx+tJ" +
            "IRa8O9cGXhCY5Q==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";

        private const string SignatureMarker = "-----SIGNATURE-----";

        public static State  Status      { get; private set; } = State.Missing;
        public static string LicensedTo  { get; private set; } = "";
        public static string Site        { get; private set; } = "";
        public static string IssuedOn    { get; private set; } = "";

        public static string FilePath =>
            Path.Combine(DatabaseManager.DataDirectory, "licence.lic");

        // The one question the rest of the application asks. Reading and exporting never consult it.
        public static bool AllowsNewRecords => Status == State.Valid;

        public static void Check()
        {
            try
            {
                if (!File.Exists(FilePath)) { Reset(State.Missing); return; }
                Apply(File.ReadAllText(FilePath));
            }
            catch { Reset(State.Unreadable); }
        }

        // Writes a licence the operator has pasted in, but only once it has been shown to be
        // genuine AND for this machine. Storing an unusable one would leave them looking at a
        // licence file while the program still refuses to work.
        public static bool Install(string licenceText, out string error)
        {
            error = null;

            State result = Evaluate(licenceText, out string to, out string site,
                                    out string issued, out string[] machine);

            if (result == State.Unreadable)
            {
                error = "That is not a valid licence. Check the whole text was copied, "
                      + "including the signature at the end.";
                return false;
            }
            if (result == State.OtherMachine)
            {
                error = "That licence is genuine but was issued for a different computer."
                      + Environment.NewLine + Environment.NewLine
                      + "It is issued to: " + (string.IsNullOrWhiteSpace(to) ? "(unnamed)" : to)
                      + Environment.NewLine + "This computer's code is:" + Environment.NewLine
                      + MachineFingerprint.MachineCode();
                return false;
            }

            try
            {
                Directory.CreateDirectory(DatabaseManager.DataDirectory);
                File.WriteAllText(FilePath, licenceText.Trim() + Environment.NewLine);
            }
            catch (Exception ex)
            {
                error = "The licence is valid but could not be saved: " + ex.Message;
                return false;
            }

            Status = State.Valid; LicensedTo = to; Site = site; IssuedOn = issued;
            return true;
        }

        // ===================================================================

        private static void Apply(string text)
        {
            Status = Evaluate(text, out string to, out string site, out string issued, out _);
            LicensedTo = to; Site = site; IssuedOn = issued;
            if (Status != State.Valid && Status != State.OtherMachine)
            {
                LicensedTo = ""; Site = ""; IssuedOn = "";
            }
        }

        private static void Reset(State s)
        {
            Status = s; LicensedTo = ""; Site = ""; IssuedOn = "";
        }

        private static State Evaluate(string text, out string to, out string site,
                                      out string issued, out string[] machine)
        {
            to = ""; site = ""; issued = ""; machine = null;
            if (string.IsNullOrWhiteSpace(text)) return State.Unreadable;

            int marker = text.IndexOf(SignatureMarker, StringComparison.Ordinal);
            if (marker < 0) return State.Unreadable;

            string body      = text.Substring(0, marker);
            string signature = text.Substring(marker + SignatureMarker.Length);

            // Normalise before verifying. A licence travels by email and through copy and paste,
            // either of which rewrites line endings; without this a perfectly genuine licence
            // would fail for a reason no operator could ever diagnose.
            string payload = Normalise(body);

            byte[] sig;
            try
            {
                string cleaned = signature.Replace("\r", "").Replace("\n", "").Replace(" ", "").Trim();
                sig = Convert.FromBase64String(cleaned);
            }
            catch { return State.Unreadable; }

            try
            {
                using (var rsa = new RSACryptoServiceProvider())
                {
                    rsa.PersistKeyInCsp = false;
                    rsa.FromXmlString(PublicKeyXml);
                    if (!rsa.VerifyData(Encoding.UTF8.GetBytes(payload), "SHA256", sig))
                        return State.Unreadable;
                }
            }
            catch { return State.Unreadable; }

            // Signature holds, so the contents can be trusted from here.
            foreach (string line in payload.Split('\n'))
            {
                int colon = line.IndexOf(':');
                if (colon < 0) continue;
                string key = line.Substring(0, colon).Trim();
                string val = line.Substring(colon + 1).Trim();

                if      (key.Equals("Licensed to", StringComparison.OrdinalIgnoreCase)) to     = val;
                else if (key.Equals("Site",        StringComparison.OrdinalIgnoreCase)) site   = val;
                else if (key.Equals("Issued",      StringComparison.OrdinalIgnoreCase)) issued = val;
                else if (key.Equals("Machine",     StringComparison.OrdinalIgnoreCase))
                    machine = MachineFingerprint.ParseMachineCode(val);
            }

            if (machine == null) return State.Unreadable;
            return IsThisMachine(machine) ? State.Valid : State.OtherMachine;
        }

        // Two of the three identifiers is enough, so a replaced disk or a reinstalled Windows does
        // not lock the operator out. Where a source cannot be read on either side it is skipped
        // rather than counted against -- on a machine with WMI disabled or a blank OEM serial,
        // matching everything that CAN be compared is the most the licence can honestly ask.
        private static bool IsThisMachine(string[] licensed)
        {
            MachineFingerprint.Compare(licensed, out int matches, out int comparable);
            if (comparable == 0) return false;          // nothing to go on: do not guess
            if (matches >= 2) return true;              // normal case, tolerant of one change
            return matches >= 1 && matches == comparable;
        }

        private static string Normalise(string s)
        {
            var sb = new StringBuilder();
            foreach (string line in s.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n'))
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(line.TrimEnd());
            }
            return sb.ToString().TrimEnd('\n');
        }
    }
}
