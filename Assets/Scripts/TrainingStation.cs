using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class TrainingStation : MonoBehaviour
{
    public TrainingType trainingType;
    public TrainingScope trainingScope;
    public string stationName;
}