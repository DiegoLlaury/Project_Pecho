using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;
[RequireComponent(typeof(PlayerInputHandler))]
public class CollectibleItemScript : MonoBehaviour
{
    public CollectibleType collectibleObject;
    private IPlayerInput input;

    private void Awake()
    {
        input = GetComponent<PlayerInputHandler>();
    }
    private void Collect()
    {
        Debug.Log("Collect executé");
        GameObject.Destroy(gameObject);
    }
    private void OnTriggerEnter(Collider other)
    {

        if (other.CompareTag("Player"))
        {
            Debug.Log("Input assigné");
            input.InteractPressed += Collect;
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("Input désassigné");
            input.InteractPressed -= Collect;
        }
    }

}
