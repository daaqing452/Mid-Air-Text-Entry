using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FingerCollider : MonoBehaviour
{
    Keyboard keyboard;
    public bool indexTipEnter = false;

    void Start() {
        keyboard = GameObject.Find("Keyboard").GetComponent<Keyboard>();
    }
    
    void Update() {
        
    }

    void OnTriggerEnter(Collider collider) {
        if (collider.name == "r_index_finger_tip_collider") {
            indexTipEnter = true;
        }
    }

    void OnTriggerStay(Collider collider) {
        
    }

    void OnTriggerExit(Collider collider) {
        if (collider.name == "r_index_finger_tip_collider") {
            indexTipEnter = false;
        }
    }
}
