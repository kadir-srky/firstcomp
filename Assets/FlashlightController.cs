using UnityEngine;
using System.Collections;

public class FlashlightController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The Spot Light component attached to this flashlight.")]
    public Light flashlightLight;

    [Header("Flashlight Settings")]
    [Tooltip("Key used to toggle the flashlight on and off.")]
    public KeyCode toggleKey = KeyCode.F;
    [Tooltip("Starting state of the flashlight when the game begins.")]
    public bool startsOn = false;

    [Header("Flicker Settings (Horror Atmosphere)")]
    [Tooltip("Enable randomized spooky flickering to build tension.")]
    public bool enableFlickering = true;
    [Tooltip("Minimum delay between random flickers.")]
    public float minFlickerDelay = 5.0f;
    [Tooltip("Maximum delay between random flickers.")]
    public float maxFlickerDelay = 15.0f;
    [Tooltip("The speed of the flicker animation.")]
    public float flickerSpeed = 0.1f;

    private bool _isOn;
    private bool _isFlickering = false;
    private float _baseIntensity;
    private float _nextFlickerTime;

    void Start()
    {
        if (flashlightLight == null)
        {
            // Try to find a Light component in children if not assigned
            flashlightLight = GetComponentInChildren<Light>();
        }

        if (flashlightLight != null)
        {
            _baseIntensity = flashlightLight.intensity;
            _isOn = startsOn;
            flashlightLight.enabled = _isOn;
        }
        else
        {
            Debug.LogError("FlashlightController: No Light component assigned or found in children!");
        }

        // Set the timer for the very first random flicker event
        ResetFlickerTimer();
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleFlashlight();
        }

        // Only handle horror flickering if the flashlight is turned on and not already running a flicker routine
        if (enableFlickering && _isOn && !_isFlickering && Time.time >= _nextFlickerTime)
        {
            StartCoroutine(SpookyFlickerRoutine());
        }
    }

    void ToggleFlashlight()
    {
        if (flashlightLight == null) return;

        _isOn = !_isOn;
        flashlightLight.enabled = _isOn;

        // If turned off, make sure we reset the intensity so it doesn't get stuck in a dim state
        if (!_isOn)
        {
            StopAllCoroutines();
            _isFlickering = false;
            flashlightLight.intensity = _baseIntensity;
        }
        else
        {
            ResetFlickerTimer();
        }
    }

    private IEnumerator SpookyFlickerRoutine()
    {
        _isFlickering = true;

        // Determine how many times it will flash during this single flicker event
        int flickerCount = Random.Range(3, 8);

        for (int i = 0; i < flickerCount; i++)
        {
            // Quickly disable/enable or dim the light intensity to simulate a failing battery or entity presence
            flashlightLight.intensity = Random.Range(0.0f, 0.2f) * _baseIntensity;
            yield return new WaitForSeconds(Random.Range(0.05f, flickerSpeed));

            flashlightLight.intensity = _baseIntensity;
            yield return new WaitForSeconds(Random.Range(0.05f, flickerSpeed));
        }

        _isFlickering = false;
        ResetFlickerTimer();
    }

    private void ResetFlickerTimer()
    {
        // Randomize when the next scary flicker will occur
        _nextFlickerTime = Time.time + Random.Range(minFlickerDelay, maxFlickerDelay);
    }
}