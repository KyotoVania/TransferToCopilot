using UnityEngine;
using Sirenix.OdinInspector;
using System.Collections;

/// <summary>
/// Version optimisée et complète de MusicReactiveEnvironment.
/// Ce script est conçu pour être une simple couche de configuration qui appelle le EnvironmentAnimationManager,
/// qui est maintenant une réplique structurelle de TileAnimationManager.
/// </summary>
public class MusicReactiveEnvironment_Optimized : Environment, IAnimatableEnvironment
{
    #region Configuration Fields
    [Title("Music Reaction Settings")]
    [SerializeField, Range(0f, 1f)]
    protected float reactionProbability = 0.65f;

    [SerializeField]
    protected bool reactToBeat = true;

    [SerializeField]
    protected EnvironmentAnimationType animationType = EnvironmentAnimationType.Stretch;

    [Title("Stretch Animation Settings")]
    [ShowIf("IsStretchAnimation")]
    [SerializeField]
    protected bool useStretchAnimation = true;

    [ShowIf("@this.IsStretchAnimation() && this.useStretchAnimation")]
    [SerializeField, LabelText("Stretch Axis")]
    protected Vector3 stretchAxis = new Vector3(0, 1, 0);

    [ShowIf("@this.IsStretchAnimation() && this.useStretchAnimation")]
    [SerializeField, Range(0.5f, 2f), LabelText("Stretch Intensity")]
    protected float stretchIntensity = 1.2f;

    [Title("Bounce Animation Settings")]
    [ShowIf("@this.IsBounceAnimation() || this.IsBounceTileReactiveAnimation()")]
    [SerializeField, Range(0.1f, 3f), LabelText("Bounce Height")]
    protected float bounceHeight = 0.5f;

    [ShowIf("@this.IsBounceAnimation() || this.IsBounceTileReactiveAnimation()")]
    [SerializeField, Range(0f, 45f), LabelText("Max Bounce Rotation")]
    protected float maxBounceRotation = 15f;

    [ShowIf("@this.IsBounceAnimation() || this.IsBounceTileReactiveAnimation()")]
    [SerializeField, LabelText("Rotation Axis")]
    protected Vector3 rotationAxis = new Vector3(1, 0, 0);

    [ShowIf("@this.IsBounceAnimation() || this.IsBounceTileReactiveAnimation()")]
    [SerializeField, LabelText("Bounce Curve")]
    protected AnimationCurve bounceCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [ShowIf("@this.IsBounceAnimation() || this.IsBounceTileReactiveAnimation()")]
    [SerializeField, LabelText("Squash On Land")]
    protected bool squashOnLand = true;

    [ShowIf("@(this.IsBounceAnimation() || this.IsBounceTileReactiveAnimation()) && this.squashOnLand")]
    [SerializeField, Range(0.5f, 1f), LabelText("Squash Factor")]
    protected float squashFactor = 0.8f;

    [Title("Common Settings")]
    [SerializeField, Range(0.1f, 2f), LabelText("Animation Duration")]
    protected float animationDuration = 0.5f;

    [SerializeField, LabelText("Animation Curve")]
    protected AnimationCurve animationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    // Note: Les autres champs complexes (TileReactive, Organic, etc.) sont omis pour se concentrer
    // sur le cœur du problème (Stretch & Bounce), mais peuvent être ajoutés de la même manière.
    #endregion

    #region Private State
    private bool isAnimating = false;
    #endregion

    #region Unity Lifecycle
    protected override IEnumerator Start()
    {
        yield return StartCoroutine(base.Start());
        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.OnBeat += HandleBeat;
        }
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.OnBeat -= HandleBeat;
        }
        if (EnvironmentAnimationManager.Instance != null)
        {
            // Assure qu'aucune animation ne reste en cours si l'objet est détruit
            EnvironmentAnimationManager.Instance.StopAllAnimationsFor(this);
        }
    }
    #endregion

    /// <summary>
    /// La méthode principale qui réagit au battement de la musique.
    /// </summary>
    private void HandleBeat(float beatDuration)
    {
        // Conditions pour ne pas jouer l'animation
        if (isAnimating || !reactToBeat || !this.enabled || !gameObject.activeInHierarchy) return;
        if (Random.value > reactionProbability) return;

        // On se marque comme "en animation" pour éviter les déclenchements multiples
        isAnimating = true;

        // On délègue entièrement la logique d'animation au manager
        switch (animationType)
        {
            case EnvironmentAnimationType.Stretch:
                if (!useStretchAnimation)
                {
                    isAnimating = false; // On n'anime pas, on se libère
                    return;
                }
                EnvironmentAnimationManager.Instance.RequestAnimation(
                    this,
                    EnvironmentAnimationManager.AnimationType.Stretch,
                    animationDuration,
                    animationCurve,
                    stretchIntensity: stretchIntensity,
                    stretchAxis: stretchAxis
                );
                break;

            case EnvironmentAnimationType.Bounce:
                EnvironmentAnimationManager.Instance.RequestAnimation(
                    this,
                    EnvironmentAnimationManager.AnimationType.Bounce,
                    animationDuration,
                    bounceCurve,
                    bounceHeight: bounceHeight,
                    bounceRotation: maxBounceRotation,
                    rotationAxis: rotationAxis,
                    squashFactor: squashOnLand ? squashFactor : 1.0f // squashFactor de 1 = pas de squash
                );
                break;
            
            // Le cas pour BounceTileReactive serait géré ici, probablement pas sur le beat
            // mais dans la méthode Update() en vérifiant le mouvement de la tuile.
            case EnvironmentAnimationType.BounceTileReactive:
                isAnimating = false; // Pour cet exemple, on ne fait rien sur le beat.
                break;
        }
    }

    /// <summary>
    /// Callback appelé par le EnvironmentAnimationManager quand l'animation est terminée.
    /// </summary>
    public void OnAnimationComplete()
    {
        isAnimating = false;
    }

    #region Odin Inspector Helpers
    protected bool IsStretchAnimation() => animationType == EnvironmentAnimationType.Stretch;
    protected bool IsBounceAnimation() => animationType == EnvironmentAnimationType.Bounce;
    protected bool IsBounceTileReactiveAnimation() => animationType == EnvironmentAnimationType.BounceTileReactive;
    #endregion
}