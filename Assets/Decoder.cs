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
            if (predictor.inputs.Count > 0) keyboard.DrawTapFeedback(predictor.inputs[predictor.inputs.Count - 1]);
            keyboard.PlayClickAudio();
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
            keyboard.PlayClickAudio();
        } else {
            if (state == (int)NaiveGestureExtractor.GestureInputState.Enter) keyboard.PlayClickAudio();
            predictor.Predict(p, state);
        }
        keyboard.DrawGestureFeedback(predictor.inputs);
    }
}