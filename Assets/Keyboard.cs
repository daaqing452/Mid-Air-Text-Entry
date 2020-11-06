using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Lexicon = System.Collections.Generic.Dictionary<string, int>;


public class Keyboard : MonoBehaviour
{
    public enum TextEntryMethod { Mixed, Tap, Gesture };
    public enum LexiconType { English, Pinyin };
    
    [Header("Configuration")]
    public TextEntryMethod textEntryMethod = TextEntryMethod.Mixed;
    public LexiconType lexicon = LexiconType.English;
    public int DictionarySize = 10000;

    [Header("Thresholds")]
    public float TypeZoneDistance = 0.03f;
    public float PinchDistance = 0.015f;

    [Header("Switches")]
    public bool VisualizeFingertip = false;
    public bool VisualizeKeyCentroids = false;

    [Header("GameObjects (Control Panel)")]
    public MeshRenderer uMixedMethod;
    public MeshRenderer uTapMethod;
    public MeshRenderer uGestureMethod;
    public MeshRenderer uEnglishLexicon;
    public MeshRenderer uPinyinLexicon;

    [Header("GameObjects (Position)")]
    public GameObject[] uKeyAnchors;
    public Text[] uCandidateTexts;
    public GameObject uMainCamera;
    public GameObject uKeyboardBase;

    [Header("GameObjects (Finger)")]
    public GameObject uLeftIndexPad;
    public GameObject uLeftIndexTip;
    public GameObject uLeftThumbPad;
    public GameObject uRightIndexPad;
    public GameObject uRightIndexTip;
    public GameObject uRightThumbPad;
    public Touch uLeftTouch;
    public Touch uRightTouch;

    [Header("GameObjects (Text)")]
    public Text uInfo;
    public Text uExampleText;
    public Text uOutputText;

    [Header("GameObjects (Feedback)")]
    public GameObject uTapFeedback;
    public LineRenderer uGestureFeedback;
    public AudioSource uClickAudio;

    // phrases
    string[] phrases;
    int phraseIdx = -1;
    Decoder decoder;

    // lexicon
    Lexicon lexiconDictEnglish;
    Lexicon lexiconDictPinyin;
    
    // log
    bool logging = true;
    string logFileName;

    // pinch detection
    int leftPinchState = 0;
    int rightPinchState = 0;

    // visual effect
    const int CURSOR_BLINK_TICKS = 60;
    int cursorBlinkCounter = 0;

    void Start() {
        // pre-configuration
        uKeyAnchors = GameObject.FindGameObjectsWithTag("Key Anchor");
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

        // load phrases and lexicon
        try {
            phrases = XFileManager.ReadLines("phrases2.txt");
            lexiconDictEnglish = new Dictionary<string, int>();
            string[] anc = XFileManager.ReadLines("ANC.txt");
            for (int i = 0; i < DictionarySize; i++) {
                string[] ssp = anc[i].Split(' ');
                lexiconDictEnglish[ssp[0]] = int.Parse(ssp[1]);
            }
            lexiconDictPinyin = new Dictionary<string, int>();
            string[] chn = XFileManager.ReadLines("dict_chn_pinyin.txt");
            for (int i = 0; i < DictionarySize; i++) {
                string[] ssp = chn[i].Split(' ');
                lexiconDictPinyin[ssp[0]] = int.Parse(ssp[1]);
            }
        } catch (Exception e) {
            Debug.Log(e);
        }
        
        // init log
        string timestamp = DateTime.Now.ToShortDateString().Replace("/", "") + "-" + DateTime.Now.ToShortTimeString().Replace(":", "");
        logFileName = "move-" + timestamp + ".txt";

        // control panel
        UpdateTextEntryMethod();
        UpdateLexicon();
    }
    
    void Update() {
        if (OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.LTouch) > 0.99f) {
            // do nothing
        }
        PinchDetection();
    }

    void FixedUpdate() {
        // cursor blink tick
        cursorBlinkCounter = (cursorBlinkCounter + 1) % CURSOR_BLINK_TICKS;

        // fade tap feedback
        Color tapFeedbackColor = uTapFeedback.GetComponent<MeshRenderer>().material.color;
        if (tapFeedbackColor.a == 0) {
            uTapFeedback.transform.position = new Vector3(0, 0, -5);
        } else {
            tapFeedbackColor.a = Math.Max(tapFeedbackColor.a - 0.02f, 0);
            uTapFeedback.GetComponent<MeshRenderer>().material.color = tapFeedbackColor;
        }

        try {
            // update finger position
            Vector4 combineL = GetFingerCombine(uLeftIndexPad);
            Vector4 combineR = GetFingerCombine(uRightIndexPad);
            decoder.Input(combineL, false, uLeftTouch);
            decoder.Input(combineR, true, uRightTouch);
            UpdateKeyboardContent();
        } catch (Exception e) {
            InfoAppend(e.ToString());
        }
    }

    public void TouchCommand(string name, Touch touch) {
        if (name == "Example Next") {
            phraseIdx = (phraseIdx + 1) % phrases.Length;
            uExampleText.text = phrases[phraseIdx];
            //XFileManager.WriteLine(logFileName, "next");
        }
        if (name == "Output Clear") {
            decoder.ClearAll();
            UpdateKeyboardContent();
        }
        if (name == "Delete Key") {
            decoder.Erase();
            UpdateKeyboardContent();
        }
        if (name.Substring(0, 9) == "Candidate") {
            decoder.Confirm(name[10] - '0');
        }
        if (name == "Enter Key") {
            decoder.Confirm();
        }
        if (name == "Tap Method" && textEntryMethod != TextEntryMethod.Tap) {
            textEntryMethod = TextEntryMethod.Tap;
            UpdateTextEntryMethod();
        }
        if (name == "Gesture Method" && textEntryMethod != TextEntryMethod.Gesture) {
            textEntryMethod = TextEntryMethod.Gesture;
            UpdateTextEntryMethod();
        }
        if (name == "Mixed Method" && textEntryMethod != TextEntryMethod.Mixed) {
            textEntryMethod = TextEntryMethod.Mixed;
            UpdateTextEntryMethod();
        }
        if (name == "English Lexicon" && lexicon != LexiconType.English) {
            lexicon = LexiconType.English;
            UpdateLexicon();
        }
        if (name == "Pinyin Lexicon" && lexicon != LexiconType.Pinyin) {
            lexicon = LexiconType.Pinyin;
            UpdateLexicon();
        }
    }

    public void InfoShow(string s) {
        uInfo.text = s;
    }

    public void InfoAppend(string s) {
        uInfo.text += "\n" + s;
    }

    void UpdateKeyboardContent() {
        string outputString = "";
        List<string> candidates = new List<string>();
        decoder.Output(ref outputString, ref candidates);
        if (cursorBlinkCounter >= CURSOR_BLINK_TICKS / 2) outputString += "|";
        uOutputText.text = outputString;
        for (int i = 0; i < 5; i++) {
            if (i >= candidates.Count) {
                uCandidateTexts[i].text = "";
            } else {
                uCandidateTexts[i].text = candidates[i];
            }
        }
    }

    void UpdateTextEntryMethod() {
        uMixedMethod.material.color = Color.white;
        uTapMethod.material.color = Color.white;
        uGestureMethod.material.color = Color.white;
        if (textEntryMethod == TextEntryMethod.Mixed) {
            uMixedMethod.material.color = Color.yellow;
            decoder = new MixedDecoder();
        }
        if (textEntryMethod == TextEntryMethod.Tap) {
            uTapMethod.material.color = Color.yellow;
            decoder = new TapDecoder();
        }
        if (textEntryMethod == TextEntryMethod.Gesture) {
            uGestureMethod.material.color = Color.yellow;
            decoder = new GestureDecoder();
        }
        UpdateLexicon();
    }

    void UpdateLexicon() {
        try {
            uEnglishLexicon.material.color = Color.white;
            uPinyinLexicon.material.color = Color.white;
            if (lexicon == LexiconType.English) {
                uEnglishLexicon.material.color = Color.yellow;
                decoder.ReloadLexicon(lexiconDictEnglish);
            }
            if (lexicon == LexiconType.Pinyin) {
                uPinyinLexicon.material.color = Color.yellow;
                decoder.ReloadLexicon(lexiconDictPinyin);
            }
        } catch (Exception e) {
            InfoAppend(e.ToString());
        }
    }

    public void DrawTapFeedback(Vector2 p) {
        uTapFeedback.transform.position = Convert2DOnKeyboardTo3D(p) + new Vector3(0, 0, -0.002f);
        Color tapFeedbackColor = uTapFeedback.GetComponent<MeshRenderer>().material.color;
        tapFeedbackColor.a = 0.8f;
        uTapFeedback.GetComponent<MeshRenderer>().material.color = tapFeedbackColor;
    }

    public void DrawGestureFeedback(List<Vector2> gesture) {
        uGestureFeedback.positionCount = gesture.Count;
        for (int i = 0; i < uGestureFeedback.positionCount; i++) {
            Vector2 v = gesture[i];
            Vector3 v3D = v.x * uKeyboardBase.transform.right.normalized + v.y * uKeyboardBase.transform.up.normalized + -0.001f * uKeyboardBase.transform.forward.normalized;
            v3D += uKeyboardBase.transform.position;
            uGestureFeedback.SetPosition(i, v3D);
        }
    }

    public void PlayClickAudio() {
        uClickAudio.Play();
    }

    void PinchDetection() {
        float leftPinchDist = (uLeftIndexPad.transform.position - uLeftThumbPad.transform.position).magnitude;
        switch (leftPinchState) {
            case 0: // no pinch
                if (leftPinchDist < PinchDistance) {
                    leftPinchState = 1;
                    // do nothing
                }
                break;
            case 1: // pinching
                if (leftPinchDist >= PinchDistance) leftPinchState = 0;
                break;
        }
        float rightPinchDist = (uRightIndexPad.transform.position - uRightThumbPad.transform.position).magnitude;
        switch (rightPinchState) {
            case 0:
                if (rightPinchDist < PinchDistance) {
                    rightPinchState = 1;
                    // do nothing
                }
                break;
            case 1:
                if (rightPinchDist >= PinchDistance) rightPinchState = 0;
                break;
        }
    }

    // get finger position
    Vector4 GetFingerCombine(GameObject finger) {
        Vector3 p = finger.transform.position;
        float dist = Vector3.Dot(p - uKeyboardBase.transform.position, -uKeyboardBase.transform.forward.normalized);
        return new Vector4(p.x, p.y, p.z, dist);
    }

    public Vector3 PointProjectOnKeyboard(Vector3 p) {
        // (p + nt - k) ⊥ n
        Vector3 k = uKeyboardBase.transform.position;
        Vector3 n = uKeyboardBase.transform.forward;
        float a = Vector3.Dot(p - k, n);
        float b = Vector3.Dot(n, n);
        float t = -a / b;
        Vector3 ans = p + n * t;
        return ans;
    }

    public Vector3 SeeThroughPointProjectOnKeyboard(Vector3 p) {
        // (p + (p - e)t - k) ⊥ n
        Vector3 e = uMainCamera.transform.position;
        Vector3 k = uKeyboardBase.transform.position;
        Vector3 n = uKeyboardBase.transform.forward;
        float a = Vector3.Dot(p - k, n);
        float b = Vector3.Dot(p - e, n);
        float t = -a / b;
        Vector3 ans = p + (p - e) * t;
        return ans;
    }

    public Vector2 Convert3DTo2DOnKeyboard(Vector3 p) {
        Vector3 q = p - uKeyboardBase.transform.position;
        float x = Vector3.Dot(q, uKeyboardBase.transform.right.normalized);
        float y = Vector3.Dot(q, uKeyboardBase.transform.up.normalized);
        return new Vector2(x, y);
    }

    public Vector3 Convert2DOnKeyboardTo3D(Vector2 p) {
        Vector3 q = p.x * uKeyboardBase.transform.right.normalized + p.y * uKeyboardBase.transform.up.normalized;
        q += uKeyboardBase.transform.position;
        return q;
    }

    public Vector2 GetTouchPosition(Vector4 p) {
        return GetDirectTouchPosition(p);
    }

    public Vector2 GetDirectTouchPosition(Vector4 p) {
        Vector3 finger = new Vector3(p.x, p.y, p.z);
        Vector3 p3D = SeeThroughPointProjectOnKeyboard(finger);
        Vector2 p2D = Convert3DTo2DOnKeyboard(p3D);
        return p2D;
    }

    public Vector2 GetHybridTouchPosition(Vector4 p, Vector2 lastTouchOnKeyboard2D) {
        Vector2 p2D = new Vector3(0, 0);
        if (p.w > 0) {
            Vector3 finger = new Vector3(p.x, p.y, p.z);
            Vector3 p3D = SeeThroughPointProjectOnKeyboard(finger);
            p2D = Convert3DTo2DOnKeyboard(p3D);
        } else {
            p2D = lastTouchOnKeyboard2D;
        }
        return p2D;
    }
}