using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Decoder {
    public const int N_CANDIDATE = 5;
    public Keyboard keyboard;
    public Extractor extractorL, extractorR;
    public Predictor predictor;
    public List<string> inputWords = new List<string>();
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

    public void ClearWord() {
        nowWord = "";
        extractorL.Clear();
        extractorR.Clear();
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
}

class TapDecoder : Decoder { 
    public TapDecoder() : base() {
        extractorL = new WhiteBoxDepthExtractor();
        extractorR = new WhiteBoxDepthExtractor();
        predictor = new TrieElasticTapPredictor(keyboard);
    }

    public override void Input(Vector4 p, bool isRight, params object[] args) {
        Extractor extractor = !isRight ? extractorL : extractorR;
        if (extractor.Input(p, args) > 0) {
            nowWord = predictor.Predict(extractor.target, args);
        }
        // draw tap touch
        if (predictor.inputs.Count > 0) {
            keyboard.tapFeedback.transform.position = keyboard.Convert2DOnKeyboardTo3D(predictor.inputs[predictor.inputs.Count - 1]);
        } else {
            keyboard.tapFeedback.transform.position = new Vector3(0, 0, -5000);
        }
    }
}

class GestureDecoder : Decoder {
    public GestureDecoder() : base() {
        extractorL = new NaiveGestureExtractor();
        extractorR = new NaiveGestureExtractor();
        predictor = new NaiveGesturePredictor(keyboard);
    }

    public override void Input(Vector4 p, bool isRight, params object[] args) {
        // one finger cannot bother the other finger
        if (!isRight && extractorR.state != (int)NaiveGestureExtractor.GestureInputState.None) return;
        if ( isRight && extractorL.state != (int)NaiveGestureExtractor.GestureInputState.None) return;
        // main
        Extractor extractor = !isRight ? extractorL : extractorR;
        int state = extractor.Input(p, args);
        if (state == (int)NaiveGestureExtractor.GestureInputState.Exit) {
            nowWord = predictor.Predict(p, state);
        } else {
            predictor.Predict(p, state);
        }
        DrawGestureTrace(predictor.inputs);
    }

    public void DrawGestureTrace(List<Vector2> gesture) {
        keyboard.gestureFeedback.positionCount = gesture.Count;
        for (int i = 0; i < keyboard.gestureFeedback.positionCount; i++) {
            Vector2 v = gesture[i];
            Vector3 v3D = v.x * keyboard.keyboardBase.transform.right.normalized + v.y * keyboard.keyboardBase.transform.up.normalized + -0.001f * keyboard.keyboardBase.transform.forward.normalized;
            v3D += keyboard.keyboardBase.transform.position;
            keyboard.gestureFeedback.SetPosition(i, v3D);
        }
    }
}