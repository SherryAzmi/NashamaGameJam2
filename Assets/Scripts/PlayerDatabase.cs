using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerDatabase", menuName = "Football/Player Database")]
public class PlayerDatabase : ScriptableObject
{
    public List<PlayerData> players = new List<PlayerData>();
}