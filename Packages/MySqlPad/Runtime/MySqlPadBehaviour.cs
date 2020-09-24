using System;
using UnityEngine;

namespace MySqlPad.Runtime
{
    public class MySqlPadBehaviour : MonoBehaviour
    {
        public static void CreateDontDestroyOnLoad() => DontDestroyOnLoad(new GameObject(nameof(MySqlPadBehaviour), typeof(MySqlPadBehaviour)));

        readonly Preference Preference = new Preference();

        ImguiMySqlPad MySqlPad;

        void OnEnable()
        {
            Preference.Load();
            if (MySqlPad == null)
                MySqlPad = new ImguiMySqlPad(Preference);
        }

        void OnDisable() => Preference.Save();

        void OnGUI() => MySqlPad.OnGUI(Screen.width, Screen.height);

        void Update() => MySqlPad.Update();
    }

    [Serializable]
    public class Preference : IMySqlPadParameter
    {
        public string ConnectionString = "Host=localhost; UID=root; AllowUserVariables=true;";
        public string SQL = @"SHOW DATABASES;";

        string IMySqlPadParameter.ConnectionString { get => ConnectionString; set => ConnectionString = value; }
        string IMySqlPadParameter.SQL { get => SQL; set => SQL = value; }

        const string KEY = nameof(Preference);

        public void Save() => PlayerPrefs.SetString(KEY, JsonUtility.ToJson(this));

        public void Load() => JsonUtility.FromJsonOverwrite(PlayerPrefs.GetString(KEY, JsonUtility.ToJson(this)), this);
    }
}