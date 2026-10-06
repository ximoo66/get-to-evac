using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class JoinServer : MonoBehaviour
{
    [SerializeField] private Image startServerImage;
    [SerializeField] private Image connectImage;
    [SerializeField] private Image startHostImage;

    public void JoinClient()
    {
        NetworkManager.Singleton.StartClient();
        Debug.Log("Connect as client...");
        connectImage.color = Color.darkCyan;
    }

    public void StartHost()
    {
        NetworkManager.Singleton.StartHost();
        Debug.Log("Start as host...");
        startHostImage.color = Color.darkCyan;
    }

    public void StartServer()
    {
        NetworkManager.Singleton.StartServer();
        Debug.Log("Start Server...");
        startServerImage.color = Color.darkCyan;
    }


    public void Start()
    {
        NetworkManager.Singleton.OnClientStarted += OnClientStarted;
    }

    private void OnClientStarted()
    {
        Debug.Log("Client successfully started...");
    }
}
