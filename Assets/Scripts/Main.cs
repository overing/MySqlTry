using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using MySqlConnector;

public sealed class Main : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AfterSceneLoad() => DontDestroyOnLoad(new GameObject(nameof(Main), typeof(Main)));

    readonly Preference Preference = new Preference();

    Vector2 ResultPosition = Vector2.zero;
    string[][] Result = new string[0][];
    Tuple<float, GUILayoutOption>[] ColumnWidths;
    bool OpenTemplateSelection;
    Coroutine QueryCoroutine;

    void Start() => Preference.Load();

    void OnDestroy() => Preference.Save();

    void OnGUI()
    {
        var gui = GUIContext.Instance;

        GUILayout.BeginVertical(gui.RootOptions);

        OnGUIControl(gui);

        if (Result.Length > 0 && Result[0].Length > 0)
            OnGUIResult(gui);

        GUILayout.EndVertical();
    }

    void OnGUIControl(GUIContext gui)
    {
        var free = QueryCoroutine == null;

        GUILayout.BeginHorizontal(gui.ExpandWidth);
        GUILayout.Label("ConnectionString", gui.DontExpandWidth);
        GUI.enabled = free;
        Preference.ConnectionString = GUILayout.TextField(Preference.ConnectionString, gui.ExpandWidth);
        GUI.enabled = true;
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("SQL", gui.DontExpandWidth))
            OpenTemplateSelection = !OpenTemplateSelection;

        GUI.enabled = free;
        if (OpenTemplateSelection)
        {
            if (GUILayout.Button("Query databases", gui.DontExpandWidth))
            {
                Preference.SQL = "SHOW DATABASES;";
                OpenTemplateSelection = false;
            }
            if (GUILayout.Button("Query tables", gui.DontExpandWidth))
            {
                Preference.SQL = @"## query tables of db
SET @arg_db = 'information_schema';
SELECT
  `TABLE_NAME` AS `Table`,
  `ENGINE` AS `Engine`,
  `TABLE_COLLATION` AS `Collation`
FROM `information_schema`.`TABLES`
WHERE `TABLE_SCHEMA` = @arg_db;";
                OpenTemplateSelection = false;
            }
            if (GUILayout.Button("Query columns", gui.DontExpandWidth))
            {
                Preference.SQL = @"## query columns of db.table
SET @arg_db = 'information_schema';
SET @arg_table = 'COLUMNS';
SELECT
  `COLUMN_NAME` AS `Column`,
  `COLUMN_TYPE` AS `Type`,
  `COLLATION_NAME` AS `Collation`
FROM `information_schema`.`COLUMNS`
WHERE `TABLE_SCHEMA` = @arg_db
  AND `TABLE_NAME` = @arg_table;";
                OpenTemplateSelection = false;
            }
        }
        else
            Preference.SQL = GUILayout.TextArea(Preference.SQL, gui.ExpandWidth);
        GUI.enabled = true;
        GUILayout.EndHorizontal();

        GUI.enabled = free;
        if (GUILayout.Button("Query", gui.ExpandWidth))
        {
            if (QueryCoroutine != null)
                StopCoroutine(QueryCoroutine);
            QueryCoroutine = StartCoroutine(Query());
        }
        GUI.enabled = true;
    }

    void OnGUIResult(GUIContext gui)
    {
        bool needCalcColumnWidth = ColumnWidths == null;
        if (needCalcColumnWidth)
            ColumnWidths = new Tuple<float, GUILayoutOption>[Result[0].Length];
        ResultPosition = GUILayout.BeginScrollView(ResultPosition, gui.ResultScrollViewOptions);
        for (var rowIndex = 0; rowIndex < Result.Length; rowIndex++)
        {
            var style = rowIndex == 0 ? GUI.skin.label : GUI.skin.textField;
            var row = Result[rowIndex];
            GUILayout.BeginHorizontal();
            for (var columnIndex = 0; columnIndex < row.Length; columnIndex++)
            {
                var value = row[columnIndex];
                if (needCalcColumnWidth)
                {
                    var size = style.CalcSize(new GUIContent(value));
                    if (ColumnWidths[columnIndex] == null || ColumnWidths[columnIndex].Item1 < size.x)
                        ColumnWidths[columnIndex] = Tuple.Create(size.x, GUILayout.Width(size.x));
                }
                var width = ColumnWidths[columnIndex];
                if (width == null || width.Item1 == 0)
                    width = ColumnWidths[columnIndex] = Tuple.Create(32f, GUILayout.Width(32));
                GUILayout.TextField(value, style, width.Item2);
            }
            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();
    }

    IEnumerator Query()
    {
        var query = QueryAsync(Preference.ConnectionString, Preference.SQL);
        yield return new WaitUntil(() => query.IsCompleted);

        if (query.IsFaulted)
        {
            var ex = query.Exception.Flatten().InnerException;
            Result = new string[][]
            {
                new [] { "Error" },
                new [] { ex.Message ?? ex.ToString() },
            };
        }
        else
            Result = query.Result;
        ResultPosition = Vector2.zero;
        ColumnWidths = null;
        QueryCoroutine = null;
    }

    static async Task<string[][]> QueryAsync(string connectionString, string sql)
    {
        var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = sql;

        var reader = await command.ExecuteReaderAsync();

        var rows = new List<string[]>();

        var columns = await reader.GetColumnSchemaAsync();
        rows.Add(columns.Select(c => c.ColumnName).ToArray());

        var row = new List<string>(columns.Count);
        while (await reader.ReadAsync())
        {
            foreach (var column in columns)
                row.Add(reader.GetValue(reader.GetOrdinal(column.ColumnName))?.ToString() ?? "(null)");

            rows.Add(row.ToArray());
            row.Clear();
        }

        return rows.ToArray();
    }
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

public class GUIContext
{
    static float BuildWidth, BuildHeight;
    static GUIContext _Instance;
    public static GUIContext Instance
    {
        get
        {
            if (_Instance == null || BuildWidth != Screen.width || BuildHeight != Screen.height)
            {
                BuildWidth = Screen.width;
                BuildHeight = Screen.height;
                return _Instance = new GUIContext();
            }
            return _Instance;
        }
    }
    public readonly GUILayoutOption ScreenWidth;
    public readonly GUILayoutOption ScreenHeight;
    public readonly GUILayoutOption ExpandWidth;
    public readonly GUILayoutOption ExpandHeight;
    public readonly GUILayoutOption DontExpandWidth;
    public readonly GUILayoutOption[] ResultScrollViewOptions;
    public readonly GUILayoutOption[] RootOptions;

    public GUIContext()
    {
        ScreenWidth = GUILayout.Width(Screen.width);
        ScreenHeight = GUILayout.Height(Screen.height);
        ExpandWidth = GUILayout.ExpandWidth(true);
        ExpandHeight = GUILayout.ExpandHeight(true);
        DontExpandWidth = GUILayout.ExpandWidth(false);
        ResultScrollViewOptions = new[]
        {
            GUILayout.MinWidth(Screen.width),
            ExpandHeight,
        };
        RootOptions = new[]
        {
            ScreenWidth,
            ScreenHeight,
        };
    }
}
