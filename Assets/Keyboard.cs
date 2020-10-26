using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public class Keyboard : MonoBehaviour
{
    public enum TextEntryMethod { Tap, Gesture };
    
    [Header("Configuration")]
    public TextEntryMethod textEntryMethod = TextEntryMethod.Tap;
    public int DictionarySize = 10000;

    [Header("Threshold")]
    public float TypeZoneDistance = 0.03f;
    public float PinchDistance = 0.015f;

    [Header("Switch")]
    public bool VisualizeFingertip = false;
    public bool VisualizeKeyCentroids = false;

    [Header("GameObjects")]
    public GameObject[] keyAnchors;
    public Text[] candidateTexts;
    public GameObject mainCamera, keyboardBase;
    public GameObject leftIndexPad, leftIndexTip, leftThumbPad;
    public GameObject rightIndexPad, rightIndexTip, rightThumbPad;
    public Touch leftIndexTipTouch, rightIndexTipTouch;
    public Text info, exampleText, outputText;
    public GameObject tapTouch;
    public LineRenderer gestureTrace;

    // phrases
    string[] phrases;
    int phraseIdx = -1;
    Decoder decoder;
    
    // log
    bool logging = true;
    string logFileName;

    // pinch detection
    int leftPinchState = 0;
    int rightPinchState = 0;

    // visual effect
    const int CURSOR_BLINK_TICKS = 60;
    int cursorBlinkCounter = 0;
    Material matYellow;

    void Start() {
        // pre-configuration
        keyAnchors = GameObject.FindGameObjectsWithTag("Key Anchor");
        GameObject.Find("Keyboard").transform.SetParent(GameObject.Find("CenterEyeAnchor").transform);
        if (!VisualizeFingertip) {
            GameObject.Find("l_index_finger_tip_sphere").SetActive(false);
            GameObject.Find("r_index_finger_tip_sphere").SetActive(false);
        }
        if (!VisualizeKeyCentroids) {
            foreach (GameObject g in GameObject.FindGameObjectsWithTag("Key Sphere")) {
                g.SetActive(false);
            }
        }
        RenewTextEntryMethod();

        // init phrases
        try {
            phrases = XFileManager.ReadLines("phrases2.txt");
        } catch (Exception e) {
            Debug.Log(e);
        }

        // init log
        string timestamp = DateTime.Now.ToShortDateString().Replace("/", "") + "-" + DateTime.Now.ToShortTimeString().Replace(":", "");
        logFileName = "move-" + timestamp + ".txt";
    }
    
    void Update() {
        if (OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.LTouch) > 0.99f) {
            // do nothing
        }

        // pinch detection
        float leftPinchDist = (leftIndexPad.transform.position - leftThumbPad.transform.position).magnitude;
        switch (leftPinchState) {
            case 0:     // no pinch
                if (leftPinchDist < PinchDistance) { leftPinchState = 1; Pinch(false); }
                break;
            case 1:     // pinching
                if (leftPinchDist >= PinchDistance) leftPinchState = 0;
                break;
        }
        float rightPinchDist = (rightIndexPad.transform.position - rightThumbPad.transform.position).magnitude;
        switch (rightPinchState) {
            case 0:
                if (rightPinchDist < PinchDistance) { rightPinchState = 1; Pinch(true); }
                break;
            case 1:
                if (rightPinchDist >= PinchDistance) rightPinchState = 0;
                break;
        }
    }

    void FixedUpdate() {
        // tickers
        cursorBlinkCounter = (cursorBlinkCounter + 1) % CURSOR_BLINK_TICKS;
        
        // get finger details
        Vector3 p = rightIndexPad.transform.position;
        float dist = Vector3.Dot(p - keyboardBase.transform.position, -keyboardBase.transform.forward.normalized);
        Vector4 combine = new Vector4(p.x, p.y, p.z, dist);

        // call method
        if (textEntryMethod == TextEntryMethod.Tap) {
            decoder.Input(combine, rightIndexTipTouch.GetLastTouchOnKeyboard2D(), rightIndexTipTouch.IfInTypeZone());
        } else {
            decoder.Input(combine, rightIndexTipTouch.IfTouchKeyboard(), rightIndexTipTouch.IfInTypeZone());
        }
        RenewKeyboardContent();
        //XFileManager.WriteLine(logFileName, "move " + Math.Round(dist, 5));
    }

    public void TouchCommand(string name) {
        if (name == "Example Next") {
            phraseIdx = (phraseIdx + 1) % phrases.Length;
            exampleText.text = phrases[phraseIdx];
            //XFileManager.WriteLine(logFileName, "next");
        }
        if (name == "Output Clear") {
            decoder.ClearAll();
            RenewKeyboardContent();
        }
        if (name == "Delete") {
            decoder.Erase();
            RenewKeyboardContent();
        }
        if (name.Substring(0, 9) == "Candidate") {
            decoder.Confirm(name[10] - '0');
        }
        if (name == "Tap Method" && textEntryMethod != TextEntryMethod.Tap) {
            textEntryMethod = TextEntryMethod.Tap;
            RenewTextEntryMethod();
        }
        if (name == "Gesture Method" && textEntryMethod != TextEntryMethod.Gesture) {
            textEntryMethod = TextEntryMethod.Gesture;
            RenewTextEntryMethod();
        }
    }

    void Pinch(bool right) {
        if (!right) {
            // do nothing
        } else {
            decoder.Confirm();
        }
    }

    public void ShowInfo(string s) {
        info.text = s;
    }

    void RenewKeyboardContent() {
        string outputString = "";
        List<string> candidates = new List<string>();
        decoder.Output(ref outputString, ref candidates);
        if (cursorBlinkCounter >= CURSOR_BLINK_TICKS / 2) outputString += "|";
        outputText.text = outputString;
        for (int i = 0; i < 5; i++) {
            if (i >= candidates.Count) {
                candidateTexts[i].text = "";
            } else {
                candidateTexts[i].text = candidates[i];
            }
        }
    }

    void RenewTextEntryMethod() {
        if (textEntryMethod == TextEntryMethod.Tap) {
            GameObject.Find("Tap Method").GetComponent<MeshRenderer>().material.color = new Color(255, 255, 0);
            GameObject.Find("Gesture Method").GetComponent<MeshRenderer>().material.color = new Color(255, 255, 255);
            tapTouch.SetActive(true);
            gestureTrace.gameObject.SetActive(false);
            decoder = new TapDecoder();
        }
        if (textEntryMethod == TextEntryMethod.Gesture) {
            GameObject.Find("Tap Method").GetComponent<MeshRenderer>().material.color = new Color(255, 255, 255);
            GameObject.Find("Gesture Method").GetComponent<MeshRenderer>().material.color = new Color(255, 255, 0);
            tapTouch.SetActive(false);
            gestureTrace.gameObject.SetActive(true);
            decoder = new GestureDecoder();
        }
    }

    public Vector3 PointProjectOnKeyboard(Vector3 p) {
        // (p + nt - k) ⊥ n
        Vector3 k = keyboardBase.transform.position;
        Vector3 n = keyboardBase.transform.forward;
        float a = Vector3.Dot(p - k, n);
        float b = Vector3.Dot(n, n);
        float t = -a / b;
        Vector3 ans = p + n * t;
        return ans;
    }

    public Vector3 SeeThroughPointProjectOnKeyboard(Vector3 p) {
        // (p + (p - e)t - k) ⊥ n
        Vector3 e = mainCamera.transform.position;
        Vector3 k = keyboardBase.transform.position;
        Vector3 n = keyboardBase.transform.forward;
        float a = Vector3.Dot(p - k, n);
        float b = Vector3.Dot(p - e, n);
        float t = -a / b;
        Vector3 ans = p + (p - e) * t;
        return ans;
    }

    public Vector2 Convert3DTo2DOnKeyboard(Vector3 p) {
        Vector3 q = p - keyboardBase.transform.position;
        float x = Vector3.Dot(q, keyboardBase.transform.right.normalized);
        float y = Vector3.Dot(q, keyboardBase.transform.up.normalized);
        return new Vector2(x, y);
    }

    public Vector3 Convert2DOnKeyboardTo3D(Vector2 p) {
        Vector3 q = p.x * keyboardBase.transform.right.normalized + p.y * keyboardBase.transform.up.normalized;
        q += keyboardBase.transform.position;
        return q;
    }
}