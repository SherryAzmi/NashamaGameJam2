using TMPro;
using UnityEngine;

// A single UI dot representing a player or the ball on the live match pitch.
// Each token has its own movement "brain": a unique noise seed/speed so it
// never sits perfectly still, an individual reaction delay so a new
// instruction doesn't apply instantly (players don't move in lockstep),
// an individual push reaction strength, and a role clamp so it can't drift
// outside the zone its position is responsible for. Motion uses SmoothDamp
// for natural ease-in/ease-out instead of a flat exponential lerp.
public class MatchDotToken : MonoBehaviour
{
    public TMP_Text numberLabel;
    public float moveSpeed = 3f;

    public float wanderRadius = 12f;
    public float wanderSpeed = 0.5f;
    public float pushMultiplier = 1f;
    public float reactionDelay = 0.15f;

    // Role clamp, in anchored-position pixel space, relative to the base
    // (role-anchored) position. Defaults wide open (ball / unclamped).
    public float roleClampX = 9999f;
    public float roleClampY = 9999f;

    private RectTransform rectTransform;
    private Vector2 targetAnchoredPosition;
    private Vector2 pendingTarget;
    private float pendingApplyTime;
    private bool hasPending;
    private Vector2 smoothVelocity;
    private float smoothTime;
    private Vector2 noiseSeed;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        noiseSeed = new Vector2(Random.Range(0f, 1000f), Random.Range(0f, 1000f));
    }

    public void SetNumber(int number)
    {
        if (numberLabel != null)
        {
            numberLabel.text = number.ToString();
        }
    }

    public void SetIndividualProfile(float moveSpeed, float wanderRadius, float wanderSpeed, float pushMultiplier, float roleClampX, float roleClampY)
    {
        this.moveSpeed = moveSpeed;
        this.wanderRadius = wanderRadius;
        this.wanderSpeed = wanderSpeed;
        this.pushMultiplier = pushMultiplier;
        this.roleClampX = roleClampX;
        this.roleClampY = roleClampY;
        this.reactionDelay = Random.Range(0.1f, 0.4f);
        smoothTime = Mathf.Clamp(1.2f / moveSpeed, 0.15f, 0.6f);
    }

    public void SetPositionImmediate(Vector2 anchoredPosition)
    {
        targetAnchoredPosition = anchoredPosition;
        pendingTarget = anchoredPosition;
        hasPending = false;

        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
        }

        rectTransform.anchoredPosition = anchoredPosition;
    }

    // The new target doesn't apply instantly - it lands after this
    // player's own reaction delay, so the whole team doesn't snap to a new
    // instruction in the same frame.
    public void SetTargetPosition(Vector2 anchoredPosition)
    {
        pendingTarget = anchoredPosition;
        pendingApplyTime = Time.time + reactionDelay;
        hasPending = true;
    }

    private void Update()
    {
        if (rectTransform == null)
        {
            return;
        }

        if (hasPending && Time.time >= pendingApplyTime)
        {
            targetAnchoredPosition = pendingTarget;
            hasPending = false;
        }

        float noiseX = Mathf.PerlinNoise(noiseSeed.x + Time.time * wanderSpeed, 0f) * 2f - 1f;
        float noiseY = Mathf.PerlinNoise(0f, noiseSeed.y + Time.time * wanderSpeed) * 2f - 1f;

        Vector2 wanderOffset = new Vector2(noiseX, noiseY) * wanderRadius;
        Vector2 clampedWander = new Vector2(
            Mathf.Clamp(wanderOffset.x, -roleClampX, roleClampX),
            Mathf.Clamp(wanderOffset.y, -roleClampY, roleClampY)
        );

        Vector2 desiredPosition = targetAnchoredPosition + clampedWander;

        rectTransform.anchoredPosition = Vector2.SmoothDamp(
            rectTransform.anchoredPosition,
            desiredPosition,
            ref smoothVelocity,
            smoothTime
        );
    }
}
