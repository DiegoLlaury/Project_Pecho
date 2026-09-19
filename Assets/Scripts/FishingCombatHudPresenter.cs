using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;



/// <summary>
/// Met à jour l'affichage de combat à partir de la session de pêche active.
/// </summary>
public sealed class FishingCombatHudPresenter : MonoBehaviour
{
    private const float PercentageMultiplier = 100f;
    private const string TensionTextFormat = "Tension : {0}%";
    private const int FillTextureSize = 1;
    private const float FillSpritePivot = 0.5f;
    private const float FillSpritePixelsPerUnit = 100f;

    [Header("Required References")]
    [SerializeField] private FishingSessionController fishingSessionController;
    [SerializeField] private TextMeshProUGUI tensionText;
    [SerializeField] private Image fishEnduranceFill;
    [SerializeField] private Transform fishEnduranceBarTransform;
    [SerializeField] private Camera worldCamera;

    private Sprite runtimeFillSprite;

    private void Awake()
    {
        EnsureEnduranceFillSprite();
        ConfigureEnduranceFill();
        ResolveWorldCamera();
        ResolveFishingSessionController();
    }

    private void LateUpdate()
    {
        ResolveFishingSessionController();

        if (fishingSessionController == null)
        {
            return;
        }

        UpdateTensionText();
        UpdateFishEnduranceBar();
        FaceWorldBarTowardsCamera();
    }

    /// <summary>
    /// Actualise immédiatement le texte de tension et la barre d'endurance du poisson.
    /// </summary>
    public void RefreshPresentation()
    {
        ResolveFishingSessionController();

        if (fishingSessionController == null)
        {
            return;
        }

        UpdateTensionText();
        UpdateFishEnduranceBar();
        FaceWorldBarTowardsCamera();
    }

    private void UpdateTensionText()
    {
        if (tensionText == null)
        {
            return;
        }

        int tensionPercentage = Mathf.RoundToInt(
            fishingSessionController.NormalizedTension *
            PercentageMultiplier);

        tensionText.text = string.Format(
            TensionTextFormat,
            tensionPercentage);
    }

    private void UpdateFishEnduranceBar()
    {
        if (fishEnduranceFill == null)
        {
            return;
        }

        fishEnduranceFill.fillAmount =
            Mathf.Clamp01(
                fishingSessionController.NormalizedEndurance);
    }

    private void FaceWorldBarTowardsCamera()
    {
        if (fishEnduranceBarTransform == null)
        {
            return;
        }

        ResolveWorldCamera();

        if (worldCamera == null)
        {
            return;
        }

        Vector3 directionFromCamera =
            fishEnduranceBarTransform.position -
            worldCamera.transform.position;

        if (directionFromCamera.sqrMagnitude <= Mathf.Epsilon)
        {
            return;
        }

        fishEnduranceBarTransform.rotation =
            Quaternion.LookRotation(
                directionFromCamera,
                worldCamera.transform.up);
    }

    private void EnsureEnduranceFillSprite()
    {
        if (fishEnduranceFill == null ||
            fishEnduranceFill.sprite != null)
        {
            return;
        }

        Texture2D fillTexture = new Texture2D(
            FillTextureSize,
            FillTextureSize,
            TextureFormat.RGBA32,
            false);

        fillTexture.SetPixel(
            0,
            0,
            Color.white);

        fillTexture.Apply();

        fillTexture.hideFlags =
            HideFlags.HideAndDontSave;

        runtimeFillSprite = Sprite.Create(
            fillTexture,
            new Rect(
                0f,
                0f,
                FillTextureSize,
                FillTextureSize),
            new Vector2(
                FillSpritePivot,
                FillSpritePivot),
            FillSpritePixelsPerUnit);

        runtimeFillSprite.hideFlags =
            HideFlags.HideAndDontSave;

        fishEnduranceFill.sprite =
            runtimeFillSprite;
    }

    private void ConfigureEnduranceFill()
    {
        if (fishEnduranceFill == null)
        {
            return;
        }

        fishEnduranceFill.type =
            Image.Type.Filled;

        fishEnduranceFill.fillMethod =
            Image.FillMethod.Horizontal;

        fishEnduranceFill.fillOrigin =
            (int)Image.OriginHorizontal.Left;
    }

    private void ResolveFishingSessionController()
    {
        if (fishingSessionController == null)
        {
            fishingSessionController =
                FindFirstObjectByType<FishingSessionController>();
        }
    }

    private void ResolveWorldCamera()
    {
        if (worldCamera == null)
        {
            worldCamera = Camera.main;
        }
    }

    private void OnValidate()
    {
        ConfigureEnduranceFill();
    }

    private void OnDestroy()
    {
        if (runtimeFillSprite != null)
        {
            Destroy(runtimeFillSprite.texture);
            Destroy(runtimeFillSprite);
        }
    }
}
