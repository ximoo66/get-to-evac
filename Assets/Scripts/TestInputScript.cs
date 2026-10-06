using UnityEngine;

public class MultiplayerInputScript : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    [SerializeField] private InputReader inputReader;
    void Start()
    {
        inputReader.MoveEvent += onMoveEvent;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    private void OnDestroy()
    {
        inputReader.MoveEvent -= onMoveEvent;
    }

    private void onMoveEvent(Vector2 direction)
    {
        Debug.Log(direction);
    }

}
