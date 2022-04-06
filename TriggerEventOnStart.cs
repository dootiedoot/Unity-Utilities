using Sirenix.OdinInspector;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TriggerEventOnStart : SerializedMonoBehaviour
{
    [SerializeField] [Required] private string eventName;

    // Start is called before the first frame update
    void Start()
    {
        EventManager.TriggerEvent(eventName);
        Destroy(gameObject);
    }
}
