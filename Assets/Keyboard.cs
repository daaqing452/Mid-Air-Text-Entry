using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public class Keyboard : MonoBehaviour
{
    // editor parameters
    public int DictionarySize = 50000;
    public float TypeZoneDist = 0.02f;
    public bool VisualizeFingertip = false;
    public bool VisualizeKeyCentroids = false;
    
    // GameObjects
    GameObject mainCamera, keyboardBase;
    public GameObject[] keyAnchors;
    GameObject leftIndexPad, leftIndexTip, leftThumb;
    GameObject rightIndexPad, rightIndexTip, rightThumb;
    Touch leftIndexTipTouch, rightIndexTipTouch;
    public Text info, exampleText, outputText;

    // phrases
    string[] phrases;
    int phraseIdx = -1;
    Decoder decoder;
    
    // log
    bool logging = true;
    string logFileName;

    // left pinch detection
    const float LEFT_PINCH_THRESHOLD = 0.015f;
    int leftPinchState = 0;

    void Start() {
        // main objects
        mainCamera   = GameObject.Find("CenterEyeAnchor");
        keyboardBase = GameObject.Find("Keyboard Base");
        keyAnchors   = GameObject.FindGameObjectsWithTag("Key Anchor");

        // fingers
        leftIndexPad  = GameObject.Find("l_index_finger_pad_marker");
        leftIndexTip  = GameObject.Find("l_index_finger_tip_marker");
        leftThumb     = GameObject.Find("l_thumb_finger_pad_marker");
        rightIndexPad = GameObject.Find("r_index_finger_pad_marker");
        rightIndexTip = GameObject.Find("r_index_finger_tip_marker");
        rightThumb    = GameObject.Find("r_thumb_finger_pad_marker");

        // touch collider
        leftIndexTipTouch  = GameObject.Find("l_index_finger_tip_touch").GetComponent<Touch>();
        rightIndexTipTouch = GameObject.Find("r_index_finger_tip_touch").GetComponent<Touch>();
        
        GameObject.Find("Keyboard").transform.SetParent(GameObject.Find("CenterEyeAnchor").transform);

        // pre-configuration
        if (!VisualizeFingertip) {
            GameObject.Find("l_index_finger_tip_sphere").SetActive(false);
            GameObject.Find("r_index_finger_tip_sphere").SetActive(false);
        }
        if (!VisualizeKeyCentroids) {
            foreach (GameObject g in GameObject.FindGameObjectsWithTag("Key Sphere")) {
                g.SetActive(false);
            }
        }

        // init phrases
        decoder = new Decoder();
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

        // left pinch detection
        float leftPinchDist = (leftIndexPad.transform.position - leftThumb.transform.position).magnitude;
        switch (leftPinchState) {
            // no pinch
            case 0:
                if (leftPinchDist < LEFT_PINCH_THRESHOLD) {
                    leftPinchState = 1;
                    LeftPinch();
                }
                break;
            // pinching
            case 1:
                if (leftPinchDist >= LEFT_PINCH_THRESHOLD) {
                    leftPinchState = 0;
                }
                break;
        }
    }

    void FixedUpdate() {
        Vector3 p = rightIndexTip.transform.position;
        float dist = Vector3.Dot(p - keyboardBase.transform.position, -keyboardBase.transform.forward.normalized);
        if (!rightIndexTipTouch.typing) dist = TypeZoneDist;
        Vector4 combine = new Vector4(p.x, p.y, p.z, dist);
        decoder.Input(combine);
        XFileManager.WriteLine(logFileName, "move " + Math.Round(dist, 5));
    }

    public void TouchCommand(string name) {
        switch (name) {
            case "Example Next":
                phraseIdx = (phraseIdx + 1) % phrases.Length;
                exampleText.text = phrases[phraseIdx];
                //XFileManager.WriteLine(logFileName, "next");
                break;
            case "Output Next":
                outputText.text = "";
                break;
        }
    }

    void LeftPinch() {
    }

    public void ShowInfo(string s) {
        info.text = s;
    }
}