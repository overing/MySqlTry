using System;
using System.Linq;
using MySqlPad.Runtime;
using UnityEditor;
using UnityEngine;

namespace MySqlPad.Editor
{
    public sealed class MySqlPadWindow : EditorWindow
    {
        [MenuItem("Window/MySql Pad")] public static void Open() => GetWindow<MySqlPadWindow>("MySql Pad").Show();

        readonly EditorPreference Preference = new EditorPreference();

        ImguiMySqlPad MySqlPad;

        void OnEnable()
        {
            Preference.Load();
            if (MySqlPad == null)
                MySqlPad = new ImguiMySqlPad(Preference);
        }

        void OnDisable() => Preference.Save();

        void OnGUI() => MySqlPad.OnGUI(position.width, position.height);

        void Update() => MySqlPad.Update();
    }

    [Serializable]
    public class EditorPreference : IMySqlPadParameter
    {
        public string ConnectionString = "Host=localhost; UID=root; AllowUserVariables=true;";
        public string SQL = @"SHOW DATABASES;";

        string IMySqlPadParameter.ConnectionString { get => ConnectionString; set => ConnectionString = value; }
        string IMySqlPadParameter.SQL { get => SQL; set => SQL = value; }

        static string _KEY;
        static string KEY
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(_KEY)) return _KEY;
                var path = System.IO.Path.GetDirectoryName(Application.dataPath);
                var raw = System.Text.Encoding.UTF8.GetBytes(path);
                var md5 = System.Security.Cryptography.MD5.Create().ComputeHash(raw);
                return typeof(EditorPreference).FullName + "." + md5;
            }
        }

        public void Save() => EditorPrefs.SetString(KEY, Encrypt(JsonUtility.ToJson(this), KEY));

        public void Load()
        {
            var json = JsonUtility.ToJson(this);
            var stored = EditorPrefs.GetString(KEY, json);
            try
            {
                json = Decrypt(stored, KEY);
                JsonUtility.FromJsonOverwrite(json, this);
            }
            catch (Exception ex)
            {
                Debug.LogErrorFormat("Decode stored connection fault: ", ex.Message);
            }
        }

        static string Decrypt(string cipherText, string passPhrase)
        {
            var cipherTextBytesWithSaltAndIv = Convert.FromBase64String(cipherText);
            var saltStringBytes = cipherTextBytesWithSaltAndIv.Take(EightDivKeySize).ToArray();
            var ivStringBytes = cipherTextBytesWithSaltAndIv.Skip(EightDivKeySize).Take(EightDivKeySize).ToArray();
            var cipherTextBytes = cipherTextBytesWithSaltAndIv.Skip(EightDivKeySize * 2).Take(cipherTextBytesWithSaltAndIv.Length - (EightDivKeySize * 2)).ToArray();
            using (var algorithm = CreateAlgorithm())
            using (var buffer = new System.IO.MemoryStream(cipherTextBytes))
            using (var decryptor = algorithm.CreateDecryptor(PasswordToKey(passPhrase, saltStringBytes), ivStringBytes))
            using (var cryptoStream = new System.Security.Cryptography.CryptoStream(buffer, decryptor, System.Security.Cryptography.CryptoStreamMode.Read))
            {
                var plainTextBytes = new byte[cipherTextBytes.Length];
                var decryptedByteCount = cryptoStream.Read(plainTextBytes, 0, plainTextBytes.Length);
                buffer.Close();
                cryptoStream.Close();
                return System.Text.Encoding.UTF8.GetString(plainTextBytes, 0, decryptedByteCount);
            }
        }

        static string Encrypt(string plainText, string passPhrase)
        {
            var saltStringBytes = Generate256BitsOfRandomEntropy();
            var ivStringBytes = Generate256BitsOfRandomEntropy();
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            using (var algorithm = CreateAlgorithm())
            using (var buffer = new System.IO.MemoryStream())
            using (var encryptor = algorithm.CreateEncryptor(PasswordToKey(passPhrase, saltStringBytes), ivStringBytes))
            using (var cryptoStream = new System.Security.Cryptography.CryptoStream(buffer, encryptor, System.Security.Cryptography.CryptoStreamMode.Write))
            {
                cryptoStream.Write(plainTextBytes, 0, plainTextBytes.Length);
                cryptoStream.FlushFinalBlock();
                var cipherTextBytes = saltStringBytes;
                cipherTextBytes = cipherTextBytes.Concat(ivStringBytes).ToArray();
                cipherTextBytes = cipherTextBytes.Concat(buffer.ToArray()).ToArray();
                buffer.Close();
                cryptoStream.Close();
                return Convert.ToBase64String(cipherTextBytes);
            }
        }

        static byte[] PasswordToKey(string passPhrase, byte[] saltStringBytes)
        {
            using (var pwd = new System.Security.Cryptography.Rfc2898DeriveBytes(passPhrase, saltStringBytes, DerivationIterations))
                return pwd.GetBytes(EightDivKeySize);
        }

        static System.Security.Cryptography.SymmetricAlgorithm CreateAlgorithm()
        {
            var algorithm = new System.Security.Cryptography.RijndaelManaged();
            algorithm.BlockSize = 256;
            algorithm.Mode = System.Security.Cryptography.CipherMode.CBC;
            algorithm.Padding = System.Security.Cryptography.PaddingMode.PKCS7;
            return algorithm;
        }

        static byte[] Generate256BitsOfRandomEntropy()
        {
            var randomBytes = new byte[32];
            using (var rngCsp = new System.Security.Cryptography.RNGCryptoServiceProvider())
                rngCsp.GetBytes(randomBytes);
            return randomBytes;
        }

        const int DerivationIterations = 1000;

        const int EightDivKeySize = 256 / 8;
    }
}