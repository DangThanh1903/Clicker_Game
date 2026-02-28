using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

public class SettingsUI : MonoBehaviour
{
    [SerializeField] private Slider musicSlider;

    [SerializeField] private Slider sfxSlider;
    [SerializeField] private TMP_Dropdown languageDropdown;
    [SerializeField] private LocaleSwitcher localeSwitcher;
    [SerializeField] private Toggle fpsToggle;
    [SerializeField] private FpsDisplay fpsDisplay;
    [Header("Gift Code")]
    [SerializeField] private TMP_InputField giftCodeInput;
    [SerializeField] private Button giftCodeRedeemButton;
    [SerializeField] private TMP_Text giftCodeStatusText;

    private const string SFXVolumeKey = "SFXVolume";

    private const string MusicVolumeKey = "MusicVolume";
    private const string ShowFpsKey = "ShowFPS";
    private readonly List<Locale> supportedLocales = new List<Locale>();
    private bool ignoreLanguageChange;
    private bool ignoreFpsChange;

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
            StartCoroutine(SetupLanguageDropdown());

        if (fpsToggle != null)
            SetupFpsToggle();

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

        if (fpsToggle != null)
            fpsToggle.onValueChanged.RemoveListener(OnFpsToggleChanged);

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

    IEnumerator SetupLanguageDropdown()
    {
        var init = LocalizationSettings.InitializationOperation;
        if (!init.IsDone)
            yield return init;

        if (localeSwitcher == null)
            localeSwitcher = FindObjectOfType<LocaleSwitcher>();

        supportedLocales.Clear();
        foreach (var loc in LocalizationSettings.AvailableLocales.Locales)
        {
            if (IsSupportedLocale(loc))
                supportedLocales.Add(loc);
        }

        if (supportedLocales.Count == 0)
        {
            Debug.LogWarning("[SettingsUI] No supported locales found for language dropdown.");
            yield break;
        }

        languageDropdown.ClearOptions();
        var options = new List<TMP_Dropdown.OptionData>();
        foreach (var loc in supportedLocales)
            options.Add(new TMP_Dropdown.OptionData(loc.LocaleName));
        languageDropdown.AddOptions(options);

        int selectedIndex = GetSelectedLocaleIndex();
        ignoreLanguageChange = true;
        languageDropdown.SetValueWithoutNotify(selectedIndex);
        ignoreLanguageChange = false;

        languageDropdown.onValueChanged.AddListener(OnLanguageDropdownChanged);
    }

    bool IsSupportedLocale(Locale loc)
    {
        if (loc == null) return false;
        string code = loc.Identifier.Code?.ToLowerInvariant();
        if (string.IsNullOrEmpty(code)) return false;
        return code.StartsWith("en") || code.StartsWith("vi");
    }

    int GetSelectedLocaleIndex()
    {
        var current = LocalizationSettings.SelectedLocale;
        if (current != null)
        {
            int idx = supportedLocales.IndexOf(current);
            if (idx >= 0) return idx;
        }

        return 0;
    }

    void OnLanguageDropdownChanged(int index)
    {
        if (ignoreLanguageChange) return;
        if (index < 0 || index >= supportedLocales.Count) return;
        var loc = supportedLocales[index];
        localeSwitcher?.SetLocaleByCode(loc.Identifier.Code);
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

    private async void OnGiftCodeRedeemClicked()
    {
        if (giftCodeInput == null) return;

        string code = giftCodeInput.text;
        if (giftCodeRedeemButton != null)
            giftCodeRedeemButton.interactable = false;

        SetGiftCodeStatus("Checking...");

        var result = await GiftCodeService.RedeemAsync(code);
        SetGiftCodeStatus(result.message);

        if (result.status == GiftCodeRedeemStatus.Success)
            giftCodeInput.text = string.Empty;

        if (giftCodeRedeemButton != null)
            giftCodeRedeemButton.interactable = true;
    }

    private void SetGiftCodeStatus(string message)
    {
        if (giftCodeStatusText != null)
            giftCodeStatusText.text = message ?? string.Empty;
    }
}
