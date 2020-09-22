using System;
using UnityEngine;

namespace MySqlPad.Runtime
{
    public class MySqlPadBehaviour : MonoBehaviour, IMySqlPadParameter
    {
        public static void CreateDontDestroyOnLoad() => DontDestroyOnLoad(new GameObject(nameof(MySqlPadBehaviour), typeof(MySqlPadBehaviour)));

        readonly Preference Preference = new Preference();

        ImguiMySqlPad MySqlPad;

        public string ConnectionString { get => Preference.ConnectionString; set => Preference.ConnectionString = value; }

        public string SQL { get => Preference.SQL; set => Preference.SQL = value; }

        void Awake() => MySqlPad = new ImguiMySqlPad(this);

        void OnEnable() => Preference.Load();

        void OnDisable() => Preference.Save();

        void OnGUI() => MySqlPad.OnGUI(Screen.width, Screen.height);

        void Update() => MySqlPad.Update();
    }

    [Serializable]
    public class Preference
    {
        public string ConnectionString = "Server=localhost; Port=3306; UserID=root; AllowUserVariables=true;";
        public string SQL = @"SHOW DATABASES;";

        const string KEY = nameof(Preference);

        public void Save() => PlayerPrefs.SetString(KEY, JsonUtility.ToJson(this));

        public void Load() => JsonUtility.FromJsonOverwrite(PlayerPrefs.GetString(KEY, JsonUtility.ToJson(this)), this);
    }
}