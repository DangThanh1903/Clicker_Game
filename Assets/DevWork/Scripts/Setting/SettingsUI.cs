using UnityEngine;
using UnityEngine.UI;

public class SettingsUI : MonoBehaviour
{
    [SerializeField] private Slider musicSlider;

    [SerializeField] private Slider sfxSlider;

    private const string SFXVolumeKey = "SFXVolume";

    private const string MusicVolumeKey = "MusicVolume";

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
    }

    private void OnDestroy()
    {
        if (musicSlider != null)
            musicSlider.onValueChanged.RemoveListener(OnMusicSliderChanged);

        if (sfxSlider != null)
            sfxSlider.onValueChanged.RemoveListener(OnSfxSliderChanged);
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
}
