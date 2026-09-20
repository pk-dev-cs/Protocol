using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Protocol
{
    /// <summary>Host-only storage. No file operations are exposed to Lua.</summary>
    public sealed class RobotProgramStorage
    {
        public string FilePath { get; }


        public RobotProgramStorage(string programId, string directory = null)

        {
            if (string.IsNullOrEmpty(programId))
                throw new ArgumentException("Brak identyfikatora programu.");
            foreach (char c in programId)
                if (!char.IsLetterOrDigit(c) && c != '-' && c != '_')
                    throw new ArgumentException("Nieprawidłowy identyfikator programu.");
            FilePath = Path.Combine(directory ?? Path.Combine(Application.persistentDataPath, "RobotPrograms"), programId + ".lua");
        }

        public bool TryLoad(out string code, out string error)

        {
            code = null;
            error = "";
            try
            {
                if (!File.Exists(FilePath))
                    return true;
                if (new FileInfo(FilePath).Length > 64000)
                    throw new IOException("Zapisany program jest zbyt duży.");
                code = File.ReadAllText(FilePath, Encoding.UTF8);
                if (code.Length > RobotLuaRuntime.MaxCodeLength)
                    throw new IOException("Zapisany program przekracza limit znaków.");
                return true;
            }
            catch (Exception exception)when (exception is IOException || exception is UnauthorizedAccessException)
            {
                code = null;
                error = "Nie udało się odczytać programu: " + exception.Message;
                return false;
            }
        }

        public bool TrySave(string code, out string error)

        {
            error = "";
            if (code == null || code.Length > RobotLuaRuntime.MaxCodeLength)
            {
                error = "Program przekracza limit znaków.";
                return false;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                string temporary = FilePath + ".tmp";
                File.WriteAllText(temporary, code, new UTF8Encoding(false));
                if (File.Exists(FilePath))
                    File.Replace(temporary, FilePath, null);
                else
                    File.Move(temporary, FilePath);
                return true;
            }
            catch (Exception exception)when (exception is IOException || exception is UnauthorizedAccessException)
            {
                error = "Nie udało się zapisać programu: " + exception.Message;
                return false;
            }
        }
    }
}
