using UnityEditor;
using UnityEngine;

public static class NationalTeamGenerator
{
    private const string TeamsFolder =
        "Assets/Data/NationalTeams";

    private const string DatabasePath =
        "Assets/Data/NationalTeamDatabase.asset";

    [MenuItem("Tools/Football/Generate National Teams")]
    public static void GenerateNationalTeams()
    {
        EnsureFolderExists(TeamsFolder);

        NationalTeamDatabase database =
            AssetDatabase.LoadAssetAtPath<NationalTeamDatabase>(
                DatabasePath
            );

        if (database == null)
        {
            database =
                ScriptableObject.CreateInstance<NationalTeamDatabase>();

            AssetDatabase.CreateAsset(database, DatabasePath);
        }

        database.teams.Clear();

        TeamDefinition[] teams =
        {
            // Arab & Asian Teams
            new TeamDefinition("Jordan", "JOR", 68, 66, 65, 67, 74, "4-3-3", "Balanced"),
            new TeamDefinition("Saudi Arabia", "KSA", 72, 71, 69, 70, 76, "4-2-3-1", "Possession"),
            new TeamDefinition("Iraq", "IRQ", 69, 68, 67, 69, 73, "4-3-3", "Balanced"),
            new TeamDefinition("Qatar", "QAT", 67, 69, 65, 68, 72, "4-4-2", "Counter Attack"),
            new TeamDefinition("UAE", "UAE", 66, 67, 66, 67, 70, "4-3-3", "Balanced"),
            new TeamDefinition("Oman", "OMA", 64, 65, 66, 65, 69, "4-4-2", "Defensive"),
            new TeamDefinition("Kuwait", "KUW", 62, 63, 62, 64, 67, "4-4-2", "Balanced"),
            new TeamDefinition("Bahrain", "BHR", 63, 64, 64, 65, 68, "4-2-3-1", "Counter Attack"),
            new TeamDefinition("Syria", "SYR", 64, 63, 65, 66, 68, "4-4-2", "Defensive"),
            new TeamDefinition("Palestine", "PLE", 63, 64, 62, 65, 70, "4-3-3", "Balanced"),
            new TeamDefinition("Lebanon", "LBN", 61, 62, 63, 64, 66, "4-4-2", "Defensive"),
            new TeamDefinition("Iran", "IRN", 76, 75, 74, 76, 80, "4-2-3-1", "Possession"),
            new TeamDefinition("Uzbekistan", "UZB", 72, 71, 69, 71, 75, "4-3-3", "Balanced"),
            new TeamDefinition("Japan", "JPN", 79, 80, 76, 78, 82, "4-2-3-1", "Possession"),
            new TeamDefinition("South Korea", "KOR", 78, 77, 74, 76, 80, "4-3-3", "Attack"),
            new TeamDefinition("Australia", "AUS", 74, 72, 75, 74, 77, "4-4-2", "Defensive"),
            new TeamDefinition("China", "CHN", 63, 64, 63, 65, 66, "4-4-2", "Balanced"),
            new TeamDefinition("India", "IND", 58, 59, 58, 60, 64, "4-4-2", "Defensive"),

            // African Teams
            new TeamDefinition("Morocco", "MAR", 82, 81, 80, 82, 84, "4-2-3-1", "Counter Attack"),
            new TeamDefinition("Egypt", "EGY", 76, 74, 73, 75, 78, "4-3-3", "Attack"),
            new TeamDefinition("Algeria", "ALG", 78, 77, 74, 76, 80, "4-3-3", "Possession"),
            new TeamDefinition("Tunisia", "TUN", 73, 72, 74, 73, 77, "4-4-2", "Defensive"),
            new TeamDefinition("Senegal", "SEN", 80, 78, 77, 79, 82, "4-3-3", "Attack"),
            new TeamDefinition("Nigeria", "NGA", 78, 75, 73, 75, 78, "4-3-3", "Attack"),
            new TeamDefinition("Cameroon", "CMR", 75, 73, 74, 75, 77, "4-4-2", "Balanced"),
            new TeamDefinition("Ghana", "GHA", 72, 71, 70, 72, 74, "4-2-3-1", "Counter Attack"),
            new TeamDefinition("Ivory Coast", "CIV", 77, 75, 74, 76, 79, "4-3-3", "Balanced"),
            new TeamDefinition("South Africa", "RSA", 70, 69, 70, 71, 73, "4-4-2", "Balanced"),

            // European Teams
            new TeamDefinition("France", "FRA", 91, 89, 87, 89, 91, "4-3-3", "Attack"),
            new TeamDefinition("England", "ENG", 89, 87, 84, 86, 88, "4-2-3-1", "Possession"),
            new TeamDefinition("Spain", "ESP", 88, 90, 84, 85, 89, "4-3-3", "Possession"),
            new TeamDefinition("Germany", "GER", 86, 85, 82, 84, 86, "4-2-3-1", "Attack"),
            new TeamDefinition("Portugal", "POR", 87, 86, 82, 85, 87, "4-3-3", "Possession"),
            new TeamDefinition("Netherlands", "NED", 85, 84, 82, 83, 85, "4-3-3", "Attack"),
            new TeamDefinition("Croatia", "CRO", 82, 84, 80, 81, 84, "4-3-3", "Possession"),
            new TeamDefinition("Italy", "ITA", 82, 81, 85, 84, 84, "3-5-2", "Defensive"),
            new TeamDefinition("Belgium", "BEL", 83, 82, 78, 82, 82, "4-2-3-1", "Attack"),
            new TeamDefinition("Switzerland", "SUI", 79, 80, 81, 80, 81, "4-4-2", "Balanced"),

            // North & South American Teams
            new TeamDefinition("Argentina", "ARG", 90, 88, 84, 88, 90, "4-3-3", "Possession"),
            new TeamDefinition("Brazil", "BRA", 89, 87, 82, 86, 88, "4-3-3", "Attack"),
            new TeamDefinition("Uruguay", "URU", 82, 80, 80, 81, 83, "4-4-2", "Counter Attack"),
            new TeamDefinition("Colombia", "COL", 81, 80, 78, 79, 81, "4-2-3-1", "Possession"),
            new TeamDefinition("United States", "USA", 78, 77, 75, 77, 79, "4-3-3", "Attack"),
            new TeamDefinition("Mexico", "MEX", 77, 76, 75, 76, 78, "4-3-3", "Balanced"),
            new TeamDefinition("Canada", "CAN", 74, 73, 70, 73, 75, "4-3-3", "Attack"),
            new TeamDefinition("Chile", "CHI", 75, 74, 73, 75, 76, "4-3-3", "Balanced"),
            new TeamDefinition("Ecuador", "ECU", 76, 75, 74, 76, 78, "4-4-2", "Counter Attack"),
            new TeamDefinition("Paraguay", "PAR", 72, 71, 74, 73, 75, "4-4-2", "Defensive")
        };

        foreach (TeamDefinition teamInfo in teams)
        {
            string assetPath =
                TeamsFolder + "/" +
                teamInfo.teamName.Replace(" ", "") +
                ".asset";

            NationalTeamData team =
                AssetDatabase.LoadAssetAtPath<NationalTeamData>(
                    assetPath
                );

            if (team == null)
            {
                team =
                    ScriptableObject.CreateInstance<NationalTeamData>();

                AssetDatabase.CreateAsset(team, assetPath);
            }

            team.teamName = teamInfo.teamName;
            team.countryCode = teamInfo.countryCode;

            team.attack = teamInfo.attack;
            team.midfield = teamInfo.midfield;
            team.defense = teamInfo.defense;
            team.goalkeeper = teamInfo.goalkeeper;
            team.chemistry = teamInfo.chemistry;

            team.preferredFormation = teamInfo.preferredFormation;
            team.playStyle = teamInfo.playStyle;

            EditorUtility.SetDirty(team);

            database.teams.Add(team);
        }

        EditorUtility.SetDirty(database);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = database;

        Debug.Log(
            "National Teams Generated! Total teams: " +
            database.teams.Count
        );
    }

    private static void EnsureFolderExists(string fullPath)
    {
        string[] folders = fullPath.Split('/');

        string currentPath = folders[0];

        for (int i = 1; i < folders.Length; i++)
        {
            string nextPath =
                currentPath + "/" + folders[i];

            if (!AssetDatabase.IsValidFolder(nextPath))
            {
                AssetDatabase.CreateFolder(
                    currentPath,
                    folders[i]
                );
            }

            currentPath = nextPath;
        }
    }

    private class TeamDefinition
    {
        public string teamName;
        public string countryCode;

        public int attack;
        public int midfield;
        public int defense;
        public int goalkeeper;
        public int chemistry;

        public string preferredFormation;
        public string playStyle;

        public TeamDefinition(
            string teamName,
            string countryCode,
            int attack,
            int midfield,
            int defense,
            int goalkeeper,
            int chemistry,
            string preferredFormation,
            string playStyle
        )
        {
            this.teamName = teamName;
            this.countryCode = countryCode;

            this.attack = attack;
            this.midfield = midfield;
            this.defense = defense;
            this.goalkeeper = goalkeeper;
            this.chemistry = chemistry;

            this.preferredFormation = preferredFormation;
            this.playStyle = playStyle;
        }
    }
}