using UnityEngine;
using TMPro;
using Unity.Netcode;

public class AmmoDisplay : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text ammoText; // assign in inspector

    private ProjectileLaunch trackedWeapon;

    private void Awake()
    {
        if (ammoText == null)
            ammoText = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        TryBindToLocalPlayer();
        // Also listen for future player spawn (in case local player isn't ready yet)
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }

    private void OnDisable()
    {
        UnbindWeapon();
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
    }

    private void OnClientConnected(ulong clientId)
    {
        // Wait for the local player's object to be available
        TryBindToLocalPlayer();
    }

    private void TryBindToLocalPlayer()
    {
        if (trackedWeapon != null) return;
        if (NetworkManager.Singleton == null) return;
        if (!NetworkManager.Singleton.IsClient) return;

        var playerObj = NetworkManager.Singleton.LocalClient?.PlayerObject;
        if (playerObj == null) return;

        trackedWeapon = playerObj.GetComponent<ProjectileLaunch>();
        if (trackedWeapon == null) return;

        trackedWeapon.OnAmmoChanged += HandleAmmoChanged;
        UpdateText(trackedWeapon.GetCurrentAmmo(), trackedWeapon.GetMagazineSize());
    }

    private void UnbindWeapon()
    {
        if (trackedWeapon != null)
            trackedWeapon.OnAmmoChanged -= HandleAmmoChanged;
        trackedWeapon = null;
    }

    private void HandleAmmoChanged(int currentAmmo, int magazineSize)
    {
        UpdateText(currentAmmo, magazineSize);
    }

    private void UpdateText(int currentAmmo, int magazineSize)
    {
        if (ammoText == null) return;
        ammoText.text = $"{currentAmmo} / {magazineSize}";
    }
}
