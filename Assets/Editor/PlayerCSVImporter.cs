using UnityEngine;
using UnityEditor;
using System.IO;

public class PlayerCSVImporter : EditorWindow
{
    private TextAsset csvFile;
    private string outputFolder = "Assets/GeneratedPlayers";

    [MenuItem("Tools/Import Players From CSV")]
    public static void ShowWindow()
    {
        GetWindow<PlayerCSVImporter>("Player CSV Importer");
    }

    private void OnGUI()
    {
        GUILayout.Label("Import Players CSV", EditorStyles.boldLabel);

        csvFile = (TextAsset)EditorGUILayout.ObjectField(
            "CSV File",
            csvFile,
            typeof(TextAsset),
            false
        );

        outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);

        if (GUILayout.Button("Import Players"))
        {
            ImportPlayers();
        }
    }

    private void ImportPlayers()
    {
        if (csvFile == null)
        {
            Debug.LogError("Please assign a CSV file.");
            return;
        }

        if (!AssetDatabase.IsValidFolder(outputFolder))
        {
            Directory.CreateDirectory(outputFolder);
            AssetDatabase.Refresh();
        }

        string[] lines = csvFile.text.Split('\n');

        // Skip header line
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();

            if (string.IsNullOrEmpty(line))
                continue;

            string[] values = line.Split(',');

            if (values.Length < 8)
            {
                Debug.LogWarning("Invalid line: " + line);
                continue;
            }

            PlayerData player = ScriptableObject.CreateInstance<PlayerData>();

            player.playerName = values[0].Trim();
            player.club = values[1].Trim();
            player.speed = int.Parse(values[2].Trim());
            player.shoot = int.Parse(values[3].Trim());
            player.defense = int.Parse(values[4].Trim());
            player.nationality = values[5].Trim();
            player.category = values[6].Trim();
            player.position = values[7].Trim();

            string safeName = player.playerName.Replace(" ", "_").Replace("/", "_");
            string assetPath = $"{outputFolder}/{safeName}.asset";

            AssetDatabase.CreateAsset(player, assetPath);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Players imported successfully!");
    }
}