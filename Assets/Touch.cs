using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Touch : MonoBehaviour
{
    Keyboard keyboard;
    public Collider nowCollider;
    public bool typing = false;

    void Start() {
        keyboard = GameObject.Find("Keyboard").GetComponent<Keyboard>();
    }
    
    void Update() { }

    void OnTriggerEnter(Collider collider) {
        if (collider.name == "Type Zone") {
            typing = true;
        }
    }

    void OnTriggerStay(Collider collider) {

    }

    void OnTriggerExit(Collider collider) {
        if (collider.name == "Type Zone") {
            typing = false;
        }
        keyboard.TouchCommand(collider.name);
    }
}
