using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Touch : MonoBehaviour
{
    Keyboard keyboard;
    bool ifInTypeZone = false;
    bool ifTouchKeyboard = false;
    Vector2 lastTouchOnKeyboard2D;
    Vector3 velocity, lastPosition = new Vector3(0, 0, 0);

    void Start() {
        keyboard = GameObject.Find("Keyboard").GetComponent<Keyboard>();
    }
    
    void FixedUpdate() {
        velocity = transform.position - lastPosition;
        lastPosition = transform.position;
    }

    public bool IfInTypeZone() {
        return ifInTypeZone;
    }

    public bool IfTouchKeyboard() {
        return ifTouchKeyboard;
    }

    public Vector2 GetLastTouchOnKeyboard2D() {
        return lastTouchOnKeyboard2D;
    }

    void OnTriggerEnter(Collider collider) {
        if (collider.name == "Type Zone") {
            ifInTypeZone = true;
        }
        if (collider.name == "Keyboard Base") {
            ifTouchKeyboard = true;
            if (ClickDown()) {
                Vector3 nowTouch = keyboard.PointProjectOnKeyboard(transform.position);
                lastTouchOnKeyboard2D = keyboard.Point3DTo2DOnKeyboard(nowTouch);
            }
        }
    }

    void OnTriggerStay(Collider collider) {
    }

    void OnTriggerExit(Collider collider) {
        if (collider.name == "Type Zone") {
            ifInTypeZone = false;
        }
        if (collider.name == "Keyboard Base") {
            ifTouchKeyboard = false;
        }
        keyboard.TouchCommand(collider.name);
    }

    bool ClickDown() {
        return Vector3.Dot(velocity, keyboard.keyboardBase.transform.forward) > 0;
    }
}
