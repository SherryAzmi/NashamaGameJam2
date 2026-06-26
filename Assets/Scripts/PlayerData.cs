using UnityEngine;

[CreateAssetMenu(fileName = "NewPlayer", menuName = "Football/Player Data")]
public class PlayerData : ScriptableObject
{
    public string playerName;
    public string club;
    public int speed;
    public int shoot;
    public int defense;
    public string nationality;
    public string category;
    public string position;
}