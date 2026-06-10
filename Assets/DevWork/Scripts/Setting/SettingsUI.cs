using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SettingsUI : MonoBehaviour
{
    [SerializeField] private Slider musicSlider;

    [SerializeField] private Slider sfxSlider;
    [SerializeField] private TMP_Dropdown languageDropdown;
    [SerializeField] private LocalizationManager localizationManager;
    [SerializeField] private LocaleSwitcher localeSwitcher;
    [SerializeField] private Toggle fpsToggle;
    [SerializeField] private Toggle cameraShakeToggle;
    [SerializeField] private FpsDisplay fpsDisplay;
    [Header("Gift Code")]
    [SerializeField] private TMP_InputField giftCodeInput;
    [SerializeField] private Button giftCodeRedeemButton;
    [SerializeField] private TMP_Text giftCodeStatusText;

    private const string SFXVolumeKey = "SFXVolume";

    private const string MusicVolumeKey = "MusicVolume";
    private const string ShowFpsKey = "ShowFPS";
    private readonly List<GameLanguage> supportedLanguages = new List<GameLanguage>();
    private bool ignoreLanguageChange;
    private bool ignoreFpsChange;
    private bool ignoreCameraShakeChange;
    private bool isRedeemingGiftCode;

    private void Awake()
    {
        if (!musicSlider)
            musicSlider = GetComponent<Slider>();
    }

    private void Start()
    {
        // ===== MUSIC =====
        float savedMusicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, 1f);
        float musicSliderValue = savedMusicVolume * 100f;
        musicSlider.SetValueWithoutNotify(musicSliderValue);

        if (VFXManager.Ins != null)
            VFXManager.Ins.SetMusicVolume(savedMusicVolume);

        musicSlider.onValueChanged.AddListener(OnMusicSliderChanged);

        // ===== SFX =====
        if (sfxSlider != null)
        {
            float savedSfxVolume = PlayerPrefs.GetFloat(SFXVolumeKey, 1f);
            float sfxSliderValue = savedSfxVolume * 100f;
            sfxSlider.SetValueWithoutNotify(sfxSliderValue);

            if (SoundEffectController.Ins != null)
                SoundEffectController.Ins.SetVolume(savedSfxVolume);

            sfxSlider.onValueChanged.AddListener(OnSfxSliderChanged);
        }

        if (languageDropdown != null)
            SetupLanguageDropdown();

        if (localizationManager != null)
            localizationManager.LanguageChanged += OnLocalizationLanguageChanged;

        if (fpsToggle != null)
            SetupFpsToggle();
        if (cameraShakeToggle != null)
            SetupCameraShakeToggle();

        if (giftCodeRedeemButton != null)
            giftCodeRedeemButton.onClick.AddListener(OnGiftCodeRedeemClicked);
    }

    private void OnDestroy()
    {
        if (musicSlider != null)
            musicSlider.onValueChanged.RemoveListener(OnMusicSliderChanged);

        if (sfxSlider != null)
            sfxSlider.onValueChanged.RemoveListener(OnSfxSliderChanged);

        if (languageDropdown != null)
            languageDropdown.onValueChanged.RemoveListener(OnLanguageDropdownChanged);

        if (localizationManager != null)
            localizationManager.LanguageChanged -= OnLocalizationLanguageChanged;

        if (fpsToggle != null)
            fpsToggle.onValueChanged.RemoveListener(OnFpsToggleChanged);
        if (cameraShakeToggle != null)
            cameraShakeToggle.onValueChanged.RemoveListener(OnCameraShakeToggleChanged);

        if (giftCodeRedeemButton != null)
            giftCodeRedeemButton.onClick.RemoveListener(OnGiftCodeRedeemClicked);
    }

    private void OnSfxSliderChanged(float sliderValue)
    {
        float volume01 = Mathf.Clamp01(sliderValue / 100f);

        PlayerPrefs.SetFloat(SFXVolumeKey, volume01);
        PlayerPrefs.Save();

        if (SoundEffectController.Ins != null)
            SoundEffectController.Ins.SetVolume(volume01);
    }

    private void OnMusicSliderChanged(float sliderValue)
    {
        // 0–100 -> 0–1
        float volume01 = Mathf.Clamp01(sliderValue / 100f);

        // Save
        PlayerPrefs.SetFloat(MusicVolumeKey, volume01);
        PlayerPrefs.Save();

        // Apply to VFXManager
        if (VFXManager.Ins != null)
        {
            VFXManager.Ins.SetMusicVolume(volume01);
        }
    }

    void SetupLanguageDropdown()
    {
        if (localizationManager == null)
            localizationManager = LocalizationManager.Ins;

        if (localizationManager == null && localeSwitcher != null)
            localizationManager = LocalizationManager.GetOrCreateInstance();

        if (localizationManager == null)
        {
            Debug.LogWarning("[SettingsUI] LocalizationManager is not assigned. Language switching is disabled.", this);
            languageDropdown.interactable = false;
            return;
        }

        supportedLanguages.Clear();
        foreach (var language in localizationManager.GetSupportedLanguages())
            supportedLanguages.Add(language);

        if (supportedLanguages.Count == 0)
        {
            Debug.LogWarning("[SettingsUI] No supported languages found for language dropdown.");
            return;
        }

        languageDropdown.ClearOptions();
        var options = new List<TMP_Dropdown.OptionData>();
        foreach (var language in supportedLanguages)
            options.Add(new TMP_Dropdown.OptionData(localizationManager.GetDisplayName(language)));
        languageDropdown.AddOptions(options);

        int selectedIndex = GetSelectedLocaleIndex();
        ignoreLanguageChange = true;
        languageDropdown.SetValueWithoutNotify(selectedIndex);
        ignoreLanguageChange = false;

        languageDropdown.onValueChanged.AddListener(OnLanguageDropdownChanged);
    }

    int GetSelectedLocaleIndex()
    {
        if (localizationManager != null)
            return Mathf.Max(0, supportedLanguages.IndexOf(localizationManager.CurrentLanguage));

        return 0;
    }

    void OnLanguageDropdownChanged(int index)
    {
        if (ignoreLanguageChange) return;
        if (index < 0 || index >= supportedLanguages.Count) return;

        var language = supportedLanguages[index];
        if (localizationManager != null)
            localizationManager.SetLanguage(language);
        else
            localeSwitcher?.SetLocaleByCode(LocalizationLanguageUtility.GetDefaultCultureCode(language));
    }

    void OnLocalizationLanguageChanged(GameLanguage language)
    {
        if (languageDropdown == null || supportedLanguages.Count == 0)
            return;

        int index = supportedLanguages.IndexOf(language);
        if (index < 0)
            return;

        ignoreLanguageChange = true;
        languageDropdown.SetValueWithoutNotify(index);
        ignoreLanguageChange = false;
    }

    void SetupFpsToggle()
    {
        bool show = PlayerPrefs.GetInt(ShowFpsKey, 0) == 1;
        ignoreFpsChange = true;
        fpsToggle.SetIsOnWithoutNotify(show);
        ignoreFpsChange = false;
        fpsToggle.onValueChanged.AddListener(OnFpsToggleChanged);
        ApplyFpsVisible(show);
    }

    void OnFpsToggleChanged(bool value)
    {
        if (ignoreFpsChange) return;
        PlayerPrefs.SetInt(ShowFpsKey, value ? 1 : 0);
        PlayerPrefs.Save();
        ApplyFpsVisible(value);
    }

    void ApplyFpsVisible(bool show)
    {
        if (fpsDisplay != null)
            fpsDisplay.SetVisible(show);
    }

    void SetupCameraShakeToggle()
    {
        bool enabled = CameraShakeController.IsEnabled();
        ignoreCameraShakeChange = true;
        cameraShakeToggle.SetIsOnWithoutNotify(enabled);
        ignoreCameraShakeChange = false;
        cameraShakeToggle.onValueChanged.AddListener(OnCameraShakeToggleChanged);
    }

    void OnCameraShakeToggleChanged(bool value)
    {
        if (ignoreCameraShakeChange) return;
        CameraShakeController.SetEnabled(value);
    }

    private void OnGiftCodeRedeemClicked()
    {
        _ = RedeemGiftCodeAsync();
    }

    private async Task RedeemGiftCodeAsync()
    {
        if (giftCodeInput == null || isRedeemingGiftCode) return;

        isRedeemingGiftCode = true;
        if (giftCodeRedeemButton != null)
            giftCodeRedeemButton.interactable = false;

        SetGiftCodeStatus("Checking...");

        try
        {
            string code = giftCodeInput.text;
            var result = await GiftCodeService.RedeemAsync(code);
            SetGiftCodeStatus(result.message);

            if (result.status == GiftCodeRedeemStatus.Success)
                giftCodeInput.text = string.Empty;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SettingsUI] Gift code redeem failed: {ex.Message}", this);
            SetGiftCodeStatus("Redeem failed.");
        }
        finally
        {
            isRedeemingGiftCode = false;
            if (giftCodeRedeemButton != null)
                giftCodeRedeemButton.interactable = true;
        }
    }

    private void SetGiftCodeStatus(string message)
    {
        if (giftCodeStatusText != null)
            giftCodeStatusText.text = message ?? string.Empty;
    }
}
