using UnityEngine;

[CreateAssetMenu(
    fileName = "NewNationalTeam",
    menuName = "Football/National Team Data"
)]
public class NationalTeamData : ScriptableObject
{
    [Header("Basic Info")]
    public string teamName;
    public string countryCode;
    public Sprite flag;

    [Header("Team Ratings")]
    [Range(1, 99)] public int attack = 70;
    [Range(1, 99)] public int midfield = 70;
    [Range(1, 99)] public int defense = 70;
    [Range(1, 99)] public int goalkeeper = 70;

    [Range(1, 100)] public int chemistry = 70;

    [Header("Tactics")]
    public string preferredFormation = "4-3-3";

    [Tooltip("Attack, Balanced, Defensive, Possession, Counter Attack")]
    public string playStyle = "Balanced";

    public int Overall
    {
        get
        {
            return Mathf.RoundToInt(
                (attack + midfield + defense + goalkeeper) / 4f
            );
        }
    }
}