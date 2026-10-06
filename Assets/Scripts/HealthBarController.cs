using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using Unity.Collections;

public class HealthBarController : NetworkBehaviour
{
    [SerializeField] private Health health;
    [SerializeField] private Slider slider;
    [SerializeField] private TMP_Text text;
    [SerializeField] private MultiplayerPlayerController myPlayerController;
    public float updateSpeed = 0.5f;

    public override void OnNetworkSpawn()
    {
        // force a name update in the beginning
        DoOnPlayerNameChanged(string.Empty, myPlayerController.PlayerName.Value);

        myPlayerController.PlayerName.OnValueChanged += DoOnPlayerNameChanged;
    
        if (!IsClient) {return; }

        health.CurrentHealth.OnValueChanged += HandleHealthChange;

        HandleHealthChange(0, (int)slider.maxValue);
        // text.text = "Player OCID#" + this.OwnerClientId.ToString();
    }

    private void DoOnPlayerNameChanged(FixedString32Bytes previousValue, FixedString32Bytes newValue)
    {
        text.text = newValue.ToString();
    }

    public override void OnDestroy()
    {
        myPlayerController.PlayerName.OnValueChanged -= DoOnPlayerNameChanged;
        health.CurrentHealth.OnValueChanged -= HandleHealthChange;
        base.OnDestroy();
    }
    public override void OnNetworkDespawn()
    {
        if (!IsClient) {return; }

        health.CurrentHealth.OnValueChanged -= HandleHealthChange;
    }

    public void HandleHealthChange(int oldHealth, int newHealth)
    {
        StartCoroutine(ChangeToNewValue(oldHealth, newHealth));
    }

    private IEnumerator ChangeToNewValue(int oldHealth, int newHealth)
    {
        float oldSliderValue = oldHealth;
        float elapsed = 0.0f;

        while (elapsed < updateSpeed)
        {
            elapsed += Time.deltaTime;
            slider.value = Mathf.Lerp(oldHealth, newHealth, elapsed / updateSpeed);
            yield return null;
        }
    }

    private void LateUpdate()
    {
        transform.LookAt(Camera.main.transform);
        transform.Rotate(0, 180, 0);
    }
}
