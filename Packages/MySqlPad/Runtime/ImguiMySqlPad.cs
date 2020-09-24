using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MySqlConnector;
using UnityEngine;

namespace MySqlPad.Runtime
{
    public interface IMySqlPadParameter
    {
        string ConnectionString { get; set; }
        string SQL { get; set; }
    }

    public sealed class ImguiMySqlPad
    {
        public ushort MaxDisplayColumns = 2048;

        IMySqlPadParameter Parameters;

        Vector2 TablePosition = Vector2.zero;
        string[][] Table = new string[0][];
        Tuple<float, GUILayoutOption>[] ColumnWidths;
        bool OpenTemplateSelection;
        IEnumerator ExecuteCoroutine;

        public ImguiMySqlPad(IMySqlPadParameter parameters)
            => Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));

        float BuildWidth, BuildHeight;
        GUIContext GUIContext;

        public void OnGUI(float displayWidth, float displayHeight)
        {
            if (GUIContext == null || BuildWidth != displayWidth || BuildHeight != displayHeight)
            {
                BuildWidth = displayWidth;
                BuildHeight = displayHeight;
                GUIContext = new GUIContext(displayWidth, displayHeight);
            }

            var gui = GUIContext;

            GUILayout.BeginVertical(gui.RootOptions);

            OnGUIControl(gui);

            if (Table.Length > 0 && Table[0].Length > 0)
            {
                if (ColumnWidths == null)
                    CalcColumnWidths(gui);

                OnGUITable(gui);
            }

            GUILayout.EndVertical();
        }

        public void Update()
        {
            var routine = ExecuteCoroutine;
            if (routine != null && !routine.MoveNext())
                ExecuteCoroutine = null;
        }

        void CalcColumnWidths(GUIContext gui)
        {
            ColumnWidths = new Tuple<float, GUILayoutOption>[Table[0].Length];
            for (var rowIndex = 0; rowIndex < Table.Length; rowIndex++)
            {
                if (rowIndex * Table[0].Length > MaxDisplayColumns)
                    break;

                var style = rowIndex == 0 ? gui.ColumnNameStyle : gui.ColumnValueStyle;
                var row = Table[rowIndex];
                for (var columnIndex = 0; columnIndex < row.Length; columnIndex++)
                {
                    var size = style.CalcSize(new GUIContent(row[columnIndex]));
                    if (ColumnWidths[columnIndex] == null || ColumnWidths[columnIndex].Item1 < size.x)
                        ColumnWidths[columnIndex] = Tuple.Create(size.x, GUILayout.Width(size.x));
                }
            }
        }

        void OnGUIControl(GUIContext gui)
        {
            var free = ExecuteCoroutine == null;

            GUILayout.BeginHorizontal(gui.ExpandWidth);
            GUILayout.Label("ConnectionString", gui.DontExpandWidth);
            GUI.enabled = free;
            Parameters.ConnectionString = GUILayout.TextField(Parameters.ConnectionString, gui.ExpandWidth);
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
                    Parameters.SQL = "SHOW DATABASES;";
                    OpenTemplateSelection = false;
                }
                if (GUILayout.Button("Query tables", gui.DontExpandWidth))
                {
                    Parameters.SQL = @"## query tables of db
SET @db = 'information_schema';
SELECT
  `TABLE_NAME` AS `Table`,
  `ENGINE` AS `Engine`,
  `TABLE_COLLATION` AS `Collation`
FROM `information_schema`.`TABLES`
WHERE `TABLE_SCHEMA` = @db;";
                    OpenTemplateSelection = false;
                }
                if (GUILayout.Button("Query columns", gui.DontExpandWidth))
                {
                    Parameters.SQL = @"## query columns of db.table
SET @db = 'information_schema';
SET @table = 'COLUMNS';
SELECT
  `COLUMN_NAME` AS `Column`,
  `COLUMN_TYPE` AS `Type`,
  `COLLATION_NAME` AS `Collation`
FROM `information_schema`.`COLUMNS`
WHERE `TABLE_SCHEMA` = @db AND `TABLE_NAME` = @table;";
                    OpenTemplateSelection = false;
                }
                if (GUILayout.Button("Query indexes", gui.DontExpandWidth))
                {
                    Parameters.SQL = @"## query indexes of db.table
SET @db = 'information_schema';
SET @table = 'COLUMNS';
SELECT
  `INDEX_NAME` AS `Index`,
  `COLUMN_NAME` AS `Column`,
  IF(`NON_UNIQUE`=1, 0, 1) AS `Unique`
FROM `information_schema`.`STATISTICS`
WHERE `TABLE_SCHEMA` = @db AND `TABLE_NAME` = @table;";
                    OpenTemplateSelection = false;
                }
            }
            else
                Parameters.SQL = GUILayout.TextArea(Parameters.SQL, gui.ExpandWidth);
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUI.enabled = free;
            if (GUILayout.Button("Execute", gui.ExpandWidth))
                ExecuteCoroutine = Execute();
            GUI.enabled = true;
        }

        void OnGUITable(GUIContext gui)
        {
            TablePosition = GUILayout.BeginScrollView(TablePosition, gui.TableScrollViewOptions);
            for (var rowIndex = 0; rowIndex < Table.Length; rowIndex++)
            {
                var style = rowIndex == 0 ? gui.ColumnNameStyle : gui.ColumnValueStyle;
                var row = Table[rowIndex];
                GUILayout.BeginHorizontal();
                for (var columnIndex = 0; columnIndex < row.Length; columnIndex++)
                {
                    var width = ColumnWidths[columnIndex];
                    if (width == null || width.Item1 == 0)
                        width = ColumnWidths[columnIndex] = Tuple.Create(32f, GUILayout.Width(32));

                    GUILayout.TextField(row[columnIndex], style, width.Item2);
                }
                GUILayout.EndHorizontal();
                if (rowIndex * Table[0].Length > MaxDisplayColumns)
                {
                    GUILayout.Label("(For performance reasons, the remaining lines will not be output...)");
                    break;
                }

            }
            GUILayout.EndScrollView();
        }

        IEnumerator Execute()
        {
            var query = QueryAsync(Parameters.ConnectionString, Parameters.SQL);
            while (!query.IsCompleted)
                yield return null;

            if (query.IsFaulted)
            {
                var ex = query.Exception.Flatten().InnerException;
                Table = new string[][]
                {
                    new [] { "Error" },
                    new [] { ex.Message ?? ex.ToString() },
                };
            }
            else
                Table = query.Result;
            TablePosition = Vector2.zero;
            ColumnWidths = null;
            ExecuteCoroutine = null;
        }

        static async Task<string[][]> QueryAsync(string connectionString, string sql)
        {
            var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = sql;

            var reader = await command.ExecuteReaderAsync();
            var columns = await reader.GetColumnSchemaAsync();

            var rows = new List<string[]>();
            rows.Add(columns.Select(c => c.ColumnName).ToArray());

            var row = new List<string>(columns.Count);
            while (await reader.ReadAsync())
            {
                foreach (var column in columns)
                    row.Add(ConvertValueToDisplay(reader.GetValue(reader.GetOrdinal(column.ColumnName))));

                rows.Add(row.ToArray());
                row.Clear();
            }

            return rows.ToArray();
        }

        static string ConvertValueToDisplay(object value)
        {
            if (value == null) return "(null)";
            if (value is DateTime dateTime) return dateTime.ToString("yyyy-MM-dd HH:mm:ss");
            var str = (value as string) ?? value.ToString();
            if (str.Length > 255) return str.Substring(0, 255) + "(...";
            return str;
        }
    }

    public class GUIContext
    {
        public readonly GUIStyle ColumnNameStyle;
        public readonly GUIStyle ColumnValueStyle;
        public readonly GUILayoutOption DisplayWidth;
        public readonly GUILayoutOption DisplayHeight;
        public readonly GUILayoutOption ExpandWidth;
        public readonly GUILayoutOption ExpandHeight;
        public readonly GUILayoutOption DontExpandWidth;
        public readonly GUILayoutOption[] TableScrollViewOptions;
        public readonly GUILayoutOption[] RootOptions;

        public GUIContext(float displayWidth, float displayHeight)
        {
            ColumnNameStyle = new GUIStyle(GUI.skin.textField);
            ColumnNameStyle.normal.background = Texture2D.blackTexture;
            ColumnNameStyle.hover.background = Texture2D.blackTexture;

            ColumnValueStyle = new GUIStyle(GUI.skin.textField);

            DisplayWidth = GUILayout.Width(displayWidth);
            DisplayHeight = GUILayout.Height(displayHeight);
            ExpandWidth = GUILayout.ExpandWidth(true);
            ExpandHeight = GUILayout.ExpandHeight(true);
            DontExpandWidth = GUILayout.ExpandWidth(false);
            TableScrollViewOptions = new[]
            {
                GUILayout.MinWidth(displayWidth),
                ExpandHeight,
            };
            RootOptions = new[]
            {
                DisplayWidth,
                DisplayHeight,
            };
        }
    }
}