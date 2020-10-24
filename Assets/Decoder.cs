using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Decoder {
    public const int N_CANDIDATE = 5;
    public Keyboard keyboard;
    public Extractor extractor;
    public Predictor predictor;
    public List<string> inputs = new List<string>();
    public string nowWord = "";

    public Decoder() {
        keyboard = GameObject.Find("Keyboard").GetComponent<Keyboard>();
    }

    ~Decoder() {
        ClearAll();
    }

    public virtual void Input(Vector4 p, params object[] args) { }

    public void Output(ref string output, ref List<string> candidates) {
        output = "";
        foreach (string s in inputs) output += s;
        output += nowWord;
        candidates.Clear();
        for (int i = 0; i < predictor.candidateWords.Count; i++) {
            candidates.Add(predictor.candidateWords[i].Key);
        }
    }

    public void ClearWord() {
        nowWord = "";
        extractor.Clear();
        predictor.Clear();
    }

    public void ClearAll() {
        inputs.Clear();
        ClearWord();
    }

    public void Confirm(int index = -1) {
        if (index == -1) {
            if (nowWord != "") inputs.Add(nowWord);
            inputs.Add(" ");
            ClearWord();
        } else {
            if (index < predictor.candidateWords.Count) {
                inputs.Add(predictor.candidateWords[index].Key);
                inputs.Add(" ");
                ClearWord();
            }
        }
    }

    public void Erase() {
        if (nowWord != "") {
            ClearWord();
        } else {
            if (inputs.Count > 0) {
                inputs.RemoveAt(inputs.Count - 1);
            }
        }
    }
}

class TapDecoder : Decoder {
    public TapDecoder() : base() {
        extractor = new WhiteBoxDepthExtractor();
        predictor = new RigidBayesianPredictor(this);
    }

    public override void Input(Vector4 p, params object[] args) {
        if (extractor.Input(p, args) > 0) {
            nowWord = predictor.Predict(extractor.target, args);
        }
    }
}

class GestureDecoder : Decoder {
    public GestureDecoder() : base() {
        extractor = new NaiveGestureExtractor();
        predictor = new NaiveGesturePredictor(this);
    }

    public override void Input(Vector4 p, params object[] args) {
        int state = extractor.Input(p, args);
        predictor.Predict(p, state);
    }
}