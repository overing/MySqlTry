using System;
using MySqlPad.Runtime;
using UnityEngine;
using UnityEditor;

namespace MySqlPad.Editor
{
    public sealed class MySqlPadWindow : EditorWindow, IMySqlPadParameter
    {
        readonly EditorPreference Preference = new EditorPreference();

        ImguiMySqlPad MySqlPad;

        public string ConnectionString { get => Preference.ConnectionString; set => Preference.ConnectionString = value; }

        public string SQL { get => Preference.SQL; set => Preference.SQL = value; }

        [MenuItem("Window/MySql Pad")]
        public static void Open() => EditorWindow.GetWindow<MySqlPadWindow>("MySql Pad").Show();

        void Awake() => MySqlPad = new ImguiMySqlPad(this);

        void OnEnable() => Preference.Load();

        void OnDisable() => Preference.Save();

        void OnGUI() => MySqlPad?.OnGUI(position.width, position.height);

        void Update() => MySqlPad?.Update();
    }

    [Serializable]
    public class EditorPreference
    {
        public string ConnectionString = "Server=localhost; Port=3306; UserID=root; AllowUserVariables=true;";
        public string SQL = @"SHOW DATABASES;";

        static readonly string KEY = typeof(EditorPreference).FullName;

        public void Save() => EditorPrefs.SetString(KEY, JsonUtility.ToJson(this));

        public void Load() => JsonUtility.FromJsonOverwrite(EditorPrefs.GetString(KEY, JsonUtility.ToJson(this)), this);
    }
}