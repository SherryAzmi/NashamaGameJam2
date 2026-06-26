using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "NationalTeamDatabase",
    menuName = "Football/National Team Database"
)]
public class NationalTeamDatabase : ScriptableObject
{
    public List<NationalTeamData> teams =
        new List<NationalTeamData>();
}