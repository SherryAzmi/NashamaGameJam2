using UnityEngine;
using UnityEditor;
using System.IO;

public class PlayerDatabaseBuilder
{
    [MenuItem("Tools/Build Player Database")]
    public static void BuildDatabase()
    {
        PlayerDatabase database = ScriptableObject.CreateInstance<PlayerDatabase>();

        string[] guids = AssetDatabase.FindAssets("t:PlayerData");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            PlayerData player = AssetDatabase.LoadAssetAtPath<PlayerData>(path);

            if (player != null)
                database.players.Add(player);
        }

        string assetPath = "Assets/PlayerDatabase.asset";

        if (File.Exists(assetPath))
        {
            AssetDatabase.DeleteAsset(assetPath);
        }

        AssetDatabase.CreateAsset(database, assetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Player Database Created! Total players: " + database.players.Count);
    }
}