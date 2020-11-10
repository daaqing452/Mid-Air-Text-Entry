using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public class Keyboard : MonoBehaviour
{
    public enum TextEntryMethod { Mixed, Tap, Gesture };
    public enum LexiconType { English, Pinyin };
    
    [Header("Configuration")]
    public TextEntryMethod textEntryMethod = TextEntryMethod.Mixed;
    public LexiconType lexicon = LexiconType.English;
    public int DictionarySize = 10000;
    public int AlwaysOnCandidateNumber = 5;
    
    [Header("Switches")]
    public bool VisualizeFingertip = false;
    public bool VisualizeKeyCentroids = false;
    public bool ShowPredictionTime = true;

    [Header("GameObjects (Control Panel)")]
    public MeshRenderer uMixedMethod;
    public MeshRenderer uTapMethod;
    public MeshRenderer uGestureMethod;
    public MeshRenderer uEnglishLexicon;
    public MeshRenderer uPinyinLexicon;

    [Header("GameObjects (Position)")]
    public GameObject[] uKeyAnchors;
    public GameObject[] uCandidates;
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
    public GameObject uGestureFeedback;
    public AudioSource uClickAudio;

    // phrases
    string[] phrases;
    int phraseIdx = -1;
    Decoder decoder;

    // lexicon
    Lexicon lexiconEnglish;
    Lexicon lexiconPinyin;
    Lexicon lexiconChinese;
    
    // log
    bool logging = true;
    string logFileName;

    // detection
    const float PINCH_DIST = 0.015f;
    int leftPinchState = 0;
    int rightPinchState = 0;

    // visual effect
    const int CURSOR_BLINK_TICKS = 60;
    int cursorBlinkCounter = 0;
    bool ifSelectingCandidate;

    void Start() {
        // pre-configuration
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
        Decoder.N_CANDIDATE = uCandidates.Length;

        // load phrases and lexicon
        phrases = XFileManager.ReadLines("phrases2.txt");
        lexiconEnglish = new Lexicon();
        string[] anc = XFileManager.ReadLines("ANC.txt");
        for (int i = 0; i < DictionarySize; i++) {
            string[] ssp = anc[i].Split(' ');
            lexiconEnglish.AddUnigram(ssp[0], int.Parse(ssp[1]));
        }
        lexiconPinyin = new Lexicon();
        lexiconChinese = new Lexicon();
        string[] chn = XFileManager.ReadLines("dict_chn_pinyin.txt");
        for (int i = 0; i < DictionarySize; i++) {
            string[] ssp = chn[i].Split(' ');
            lexiconPinyin.AddUnigram(ssp[0], int.Parse(ssp[1]));
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

        // update finger position
        if (!DisableInput()) {
            Vector4 combineL = GetFingerCombine(uLeftIndexPad);
            Vector4 combineR = GetFingerCombine(uRightIndexPad);
            decoder.Input(combineL, false, uLeftTouch);
            decoder.Input(combineR, true, uRightTouch);
        }
        UpdateKeyboardContent();
    }

    public void TouchCommand(string name, Touch touch) {
        switch (name) {
            case "Example Next":
                phraseIdx = (phraseIdx + 1) % phrases.Length;
                uExampleText.text = phrases[phraseIdx];
                break;
            case "Output Clear":
                decoder.ClearAll();
                UpdateKeyboardContent();
                InfoShow("");
                break;
            case "Delete Key":
                decoder.Erase();
                UpdateKeyboardContent();
                break;
            case "Enter Key":
                Confirm();
                ifSelectingCandidate = false;
                break;
            case "Candidate List Expand":
                if (decoder.nowWord != "") {
                    ifSelectingCandidate = !ifSelectingCandidate;
                }
                break;
            case "Tap Method":
                if (textEntryMethod != TextEntryMethod.Tap) {
                    textEntryMethod = TextEntryMethod.Tap;
                    UpdateTextEntryMethod();
                }
                break;
            case "Gesture Method":
                if (textEntryMethod != TextEntryMethod.Gesture) {
                    textEntryMethod = TextEntryMethod.Gesture;
                    UpdateTextEntryMethod();
                }
                break;
            case "Mixed Method":
                if (textEntryMethod != TextEntryMethod.Mixed) {
                    textEntryMethod = TextEntryMethod.Mixed;
                    UpdateTextEntryMethod();
                }
                break;
            case "English Lexicon":
                if (lexicon != LexiconType.English) {
                    lexicon = LexiconType.English;
                    UpdateLexicon();
                }
                break;
            case "Pinyin Lexicon":
                if (lexicon != LexiconType.Pinyin) {
                    lexicon = LexiconType.Pinyin;
                    UpdateLexicon();
                }
                break;
            default:
                if (name.Substring(0, 9) == "Candidate") {
                    Confirm(name[10] - '0');
                }
                break;
        }
    }

    public void InfoShow(string s) {
        uInfo.text = s;
    }

    public void InfoAppend(string s) {
        uInfo.text += "\n" + s;
    }

    void Confirm(int index = -1) {
        decoder.Confirm(index);
        ifSelectingCandidate = false;
    }

    public bool DisableInput() {
        return ifSelectingCandidate;
    }

    void UpdateKeyboardContent() {
        string outputString = "";
        List<string> candidates = new List<string>();
        decoder.Output(ref outputString, ref candidates);
        if (cursorBlinkCounter >= CURSOR_BLINK_TICKS / 2) outputString += "|";
        uOutputText.text = outputString;
        for (int i = 0; i < AlwaysOnCandidateNumber; i++) {
            uCandidates[i].GetComponentInChildren<Text>().text = i >= candidates.Count ? "" : candidates[i];
        }
        if (ifSelectingCandidate) {
            for (int i = AlwaysOnCandidateNumber; i < uCandidates.Length; i++) {
                uCandidates[i].SetActive(true);
                uCandidates[i].GetComponentInChildren<Text>().text = i >= candidates.Count ? "" : candidates[i];
            }
            uKeyboardBase.SetActive(false);
            uGestureFeedback.SetActive(false);
        } else {
            for (int i = AlwaysOnCandidateNumber; i < uCandidates.Length; i++) uCandidates[i].SetActive(false);
            uKeyboardBase.SetActive(true);
            uGestureFeedback.SetActive(true);
        }
    }

    void UpdateTextEntryMethod() {
        try {
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
        } catch (Exception e) {
            InfoAppend(e.ToString());
        }
    }

    void UpdateLexicon() {
        uEnglishLexicon.material.color = Color.white;
        uPinyinLexicon.material.color = Color.white;
        if (lexicon == LexiconType.English) {
            uEnglishLexicon.material.color = Color.yellow;
            decoder.ReloadLexicon(lexiconEnglish);
        }
        if (lexicon == LexiconType.Pinyin) {
            uPinyinLexicon.material.color = Color.yellow;
            decoder.ReloadLexicon(lexiconPinyin);
        }
    }

    public void DrawTapFeedback(Vector2 p) {
        uTapFeedback.transform.position = Convert2DOnKeyboardTo3D(p) + new Vector3(0, 0, -0.002f);
        Color tapFeedbackColor = uTapFeedback.GetComponent<MeshRenderer>().material.color;
        tapFeedbackColor.a = 0.8f;
        uTapFeedback.GetComponent<MeshRenderer>().material.color = tapFeedbackColor;
    }

    public void DrawGestureFeedback(List<Vector2> gesture) {
        LineRenderer renderer = uGestureFeedback.GetComponent<LineRenderer>();
        renderer.positionCount = gesture.Count;
        for (int i = 0; i < renderer.positionCount; i++) {
            Vector2 v = gesture[i];
            Vector3 v3D = v.x * uKeyboardBase.transform.right.normalized + v.y * uKeyboardBase.transform.up.normalized + -0.001f * uKeyboardBase.transform.forward.normalized;
            v3D += uKeyboardBase.transform.position;
            renderer.SetPosition(i, v3D);
        }
    }

    public void PlayClickAudio() {
        uClickAudio.Play();
    }

    void PinchDetection() {
        float leftPinchDist = (uLeftIndexPad.transform.position - uLeftThumbPad.transform.position).magnitude;
        switch (leftPinchState) {
            case 0: // no pinch
                if (leftPinchDist < PINCH_DIST) {
                    leftPinchState = 1;
                    // do nothing
                }
                break;
            case 1: // pinching
                if (leftPinchDist >= PINCH_DIST) leftPinchState = 0;
                break;
        }
        float rightPinchDist = (uRightIndexPad.transform.position - uRightThumbPad.transform.position).magnitude;
        switch (rightPinchState) {
            case 0:
                if (rightPinchDist < PINCH_DIST) {
                    rightPinchState = 1;
                    // do nothing
                }
                break;
            case 1:
                if (rightPinchDist >= PINCH_DIST) rightPinchState = 0;
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