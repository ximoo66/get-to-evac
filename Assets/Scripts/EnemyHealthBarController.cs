using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class EnemyHealthBarController : NetworkBehaviour
{
    [SerializeField] private Health health;           // reference to the enemy Health component
    [SerializeField] private Slider slider;           // UI slider for the health bar
    [SerializeField] private TMP_Text text;           // optional text to show numeric health (can be null)
    [SerializeField] private float updateSpeed = 0.5f; // interpolation speed

    private Coroutine _currentCoroutine;

    public override void OnNetworkSpawn()
    {
        if (health == null || slider == null)
        {
            Debug.LogWarning("EnemyHealthBarController: Health or Slider not assigned.");
            return;
        }

        // Initialize slider max value from health if available (assumes Health exposes MaximumHealth int)
        if (health.MaximumHealth > 0)
        {
            slider.maxValue = health.MaximumHealth;
        }

        // Only clients need to observe network variable changes for display
        if (!IsClient) return;

        health.CurrentHealth.OnValueChanged += OnHealthChanged;

        // Initialize display to current value
        int current = health.CurrentHealth.Value;
        slider.value = current;
        if (text != null) text.text = $"{current}/{health.MaximumHealth}";
    }

    public override void OnNetworkDespawn()
    {
        if (!IsClient || health == null) return;
        health.CurrentHealth.OnValueChanged -= OnHealthChanged;
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            // safe unsubscribe in case object destroyed before network despawn
            health.CurrentHealth.OnValueChanged -= OnHealthChanged;
        }
    }

    private void OnHealthChanged(int oldValue, int newValue)
    {
        // stop previous coroutine if running
        if (_currentCoroutine != null) StopCoroutine(_currentCoroutine);
        _currentCoroutine = StartCoroutine(AnimateHealthChange(oldValue, newValue));

        if (text != null)
        {
            text.text = $"{newValue}/{health.MaximumHealth}";
        }
    }

    private IEnumerator AnimateHealthChange(int from, int to)
    {
        float elapsed = 0f;
        while (elapsed < updateSpeed)
        {
            elapsed += Time.deltaTime;
            slider.value = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / updateSpeed));
            yield return null;
        }

        slider.value = to;
        _currentCoroutine = null;
    }

    private void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null) return;
        transform.LookAt(cam.transform);
        transform.Rotate(0f, 180f, 0f);
    }
}
