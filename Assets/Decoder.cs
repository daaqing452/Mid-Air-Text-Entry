using System.Collections.Generic;
using UnityEngine;

public class Decoder {
    public static int N_CANDIDATE = 5;
    protected Keyboard keyboard;
    protected Predictor predictor;
    protected List<string> inputWords = new List<string>();
    public string nowWord = "";

    public Decoder() {
        keyboard = GameObject.Find("Keyboard").GetComponent<Keyboard>();
    }

    ~Decoder() {
        ClearAll();
    }

    public virtual void Input(Vector4 p, bool isRight, params object[] args) { }

    public void Output(ref string output, ref List<string> candidates) {
        output = "";
        foreach (string s in inputWords) output += s;
        output += nowWord;
        candidates.Clear();
        for (int i = 0; i < predictor.candidateWords.Count; i++) {
            candidates.Add(predictor.candidateWords[i].Key);
        }
    }

    public virtual void ClearWord() {
        nowWord = "";
        predictor.Clear();
    }

    public void ClearAll() {
        inputWords.Clear();
        ClearWord();
    }

    public void Confirm(int index = -1) {
        if (index == -1) {
            if (nowWord != "") inputWords.Add(nowWord);
            inputWords.Add(" ");
            ClearWord();
        } else {
            if (index < predictor.candidateWords.Count) {
                inputWords.Add(predictor.candidateWords[index].Key);
                inputWords.Add(" ");
                ClearWord();
            }
        }
    }

    public void Erase() {
        if (nowWord != "") {
            ClearWord();
        } else {
            if (inputWords.Count > 0) {
                inputWords.RemoveAt(inputWords.Count - 1);
            }
        }
    }

    public virtual void ReloadLexicon(Lexicon lexicon) {
        predictor.ReloadLexicon(lexicon);
        ClearAll();
    }
}

class TapDecoder : Decoder {
    WhiteBoxDepthTapExtractor extractorL, extractorR;

    public TapDecoder() : base() {
        extractorL = new WhiteBoxDepthTapExtractor(keyboard);
        extractorR = new WhiteBoxDepthTapExtractor(keyboard);
        predictor = new TrieElasticTapPredictor(keyboard);
    }

    public override void Input(Vector4 p, bool isRight, params object[] args) {
        if (keyboard.DisableInput()) return;
        WhiteBoxDepthTapExtractor extractor = !isRight ? extractorL : extractorR;
        int state = extractor.Input(p, args);
        if (state == (int)WhiteBoxDepthTapExtractor.TapInputState.LiftUp) {
            nowWord = predictor.Predict(extractor.target, args);
            keyboard.DrawTapFeedback(predictor.inputs[predictor.inputs.Count - 1]);
            keyboard.PlayClickAudio();
        }
    }

    public override void ClearWord() {
        base.ClearWord();
        extractorL.Clear();
        extractorR.Clear();
    }
}

class GestureDecoder : Decoder {
    NaiveGestureExtractor extractor;
    bool nowIsRight;

    public GestureDecoder() : base() {
        extractor = new NaiveGestureExtractor(keyboard);
        predictor = new TwoLevelGesturePredictor(keyboard);
    }

    public override void Input(Vector4 p, bool isRight, params object[] args) {
        if (keyboard.DisableInput()) return;
        // one finger cannot bother the other finger
        if (isRight != nowIsRight && extractor.state != (int)NaiveGestureExtractor.GestureInputState.None) return;
        int state = extractor.Input(p, args);
        nowIsRight = isRight;
        nowWord = predictor.Predict(p, state);
        if (state == (int)NaiveGestureExtractor.GestureInputState.Enter) keyboard.PlayClickAudio();
        keyboard.DrawGestureFeedback(predictor.inputs);
    }

    public override void ClearWord() {
        base.ClearWord();
        extractor.Clear();
    }
}

class MixedDecoder : Decoder {
    public enum MixedDecoderState { Uncertain, Tap, Gesture };
    const float GESTURE_INPUT_DIST = 0.025f;

    WhiteBoxDepthTapExtractor tapExtractorL, tapExtractorR;
    NaiveGestureExtractor gestureExtractor;
    TrieElasticTapPredictor tapPredictor;
    NaiveGesturePredictor gesturePredictor;
    MixedDecoderState state;
    bool gestureInputIsRight;

    // distinguish tap & gesture
    bool inTypeZoneL, inTypeZoneR;
    bool ifTouchKeyboard;
    bool touchKeyboardIsRight;
    Vector2 firstTouch2D;
    bool ifTouchKeyboardPlayClickAudio;

    public MixedDecoder() : base() {
        tapExtractorL = new WhiteBoxDepthTapExtractor(keyboard);
        tapExtractorR = new WhiteBoxDepthTapExtractor(keyboard);
        gestureExtractor = new NaiveGestureExtractor(keyboard);
        tapPredictor = new TrieElasticTapPredictor(keyboard);
        gesturePredictor = new TwoLevelGesturePredictor(keyboard);
        predictor = tapPredictor;
        ClearWord();
    }

    public override void Input(Vector4 p, bool isRight, params object[] args) {
        if (state == MixedDecoderState.Uncertain) {
            WhiteBoxDepthTapExtractor tapExtractor = !isRight ? tapExtractorL : tapExtractorR;
            int tapState = tapExtractor.Input(p, args);
            // detected tap
            if (tapState == (int)WhiteBoxDepthTapExtractor.TapInputState.LiftUp) {
                nowWord = tapPredictor.Predict(tapExtractor.target, args);
                keyboard.DrawTapFeedback(tapPredictor.inputs[tapPredictor.inputs.Count - 1]);
                if (!ifTouchKeyboardPlayClickAudio) keyboard.PlayClickAudio();
                state = MixedDecoderState.Tap;
            }
            // both finger in type zone: must be tap
            if (tapState == (int)WhiteBoxDepthTapExtractor.TapInputState.InTypeZone) {
                if (!isRight) inTypeZoneL = true; else inTypeZoneR = true;
                if (inTypeZoneL && inTypeZoneR) state = MixedDecoderState.Tap;
            }
            // one finger touched keyboard
            if (tapState == (int)WhiteBoxDepthTapExtractor.TapInputState.TouchKeyboard) {
                if (!ifTouchKeyboard) {
                    ifTouchKeyboard = true;
                    touchKeyboardIsRight = isRight;
                    firstTouch2D = keyboard.GetTouchPosition(p);
                    keyboard.PlayClickAudio();
                    ifTouchKeyboardPlayClickAudio = true;
                }
            }
            // detect if move like gesture
            if (ifTouchKeyboard && touchKeyboardIsRight == isRight) {
                gesturePredictor.Predict(p, (int)NaiveGestureExtractor.GestureInputState.Stay);
                float moveDist = (keyboard.GetTouchPosition(p) - firstTouch2D).magnitude;
                if (moveDist > GESTURE_INPUT_DIST) {
                    state = MixedDecoderState.Gesture;
                }
            }
        }
        else
        if (state == MixedDecoderState.Tap) {
            predictor = tapPredictor;
            WhiteBoxDepthTapExtractor tapExtractor = !isRight ? tapExtractorL : tapExtractorR;
            int tapState = tapExtractor.Input(p, args);
            if (tapState == (int)WhiteBoxDepthTapExtractor.TapInputState.LiftUp) {
                nowWord = tapPredictor.Predict(tapExtractor.target, args);
                keyboard.DrawTapFeedback(tapPredictor.inputs[tapPredictor.inputs.Count - 1]);
                keyboard.PlayClickAudio();
            }
        }
        else
        if (state == MixedDecoderState.Gesture) {
            predictor = gesturePredictor;
            if (isRight != gestureInputIsRight && gestureExtractor.state != (int)NaiveGestureExtractor.GestureInputState.None) return;
            int gestureState = gestureExtractor.Input(p, args);
            gestureInputIsRight = isRight;
            nowWord = gesturePredictor.Predict(p, gestureState);
            //if (gestureState == (int)NaiveGestureExtractor.GestureInputState.Exit) keyboard.PlayClickAudio();
            keyboard.DrawGestureFeedback(gesturePredictor.inputs);
        }
    }

    public override void ClearWord() {
        base.ClearWord();
        tapExtractorL.Clear();
        tapExtractorR.Clear();
        gestureExtractor.Clear();
        tapPredictor.Clear();
        gesturePredictor.Clear();
        keyboard.DrawGestureFeedback(gesturePredictor.inputs);
        state = MixedDecoderState.Uncertain;
        inTypeZoneL = inTypeZoneR = false;
        ifTouchKeyboard = false;
        ifTouchKeyboardPlayClickAudio = false;
    }

    public override void ReloadLexicon(Lexicon lexicon) {
        base.ReloadLexicon(lexicon);
        tapPredictor.ReloadLexicon(lexicon);
        gesturePredictor.ReloadLexicon(lexicon);
    }
}