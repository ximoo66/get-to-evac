using TMPro;
using UnityEngine;

public class MainMenuController : MonoBehaviour
{

    [SerializeField] private TMP_InputField joinCodeField;

    // must be async because allocation is async!!
    public async void StartHost()
    {
        await HostSingleton.Instance.GameManager.StartHostAsync();
    }

    public async void StartClient()
    {
        await ClientSingleton.Instance.GameManager.StartClientAsync(joinCodeField.text);
    }

}
