using UnityEngine;

public class Lever : MonoBehaviour
{
    public Light[] lightsToEnable;

    private bool playerNear = false;
    private bool activated = false;

    void Update()
    {
        if (playerNear && !activated && Input.GetKeyDown(KeyCode.E))
        {
            activated = true;

            foreach (Light lightSource in lightsToEnable)
            {
                if (lightSource != null)
                {
                    lightSource.enabled = true;
                }
            }

            Debug.Log("Power Restored");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("Player Near Lever");
        if (other.CompareTag("Player"))
        {
            playerNear = true;
            
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerNear = false;
        }
    }
}
