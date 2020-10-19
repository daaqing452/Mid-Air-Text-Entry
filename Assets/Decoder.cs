using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Decoder
{
    // Lexicon
    Dictionary<string, int> freq = new Dictionary<string, int>();

    // Component
    public Keyboard keyboard;
    public Extractor extractor;
    public Predictor predictor;

    public Decoder() {
        keyboard = GameObject.Find("Keyboard").GetComponent<Keyboard>();
        extractor = new WhiteBoxExtractor(this);
        predictor = new NaivePredictor(this);

        // Load lexicon files
        try {
            string[] anc = XFileManager.ReadLines("ANC.txt");
            for (int i = 0; i < keyboard.DictionarySize; i++) {
                string[] ssp = anc[i].Split(' ');
                freq[ssp[0]] = int.Parse(ssp[1]);
            }
        } catch (Exception e) {
            Debug.Log(e);
        }
    }

    public void Input(Vector4 p) {
        if (extractor.Input(p)) {
            predictor.Predict(extractor.target);
        }
    }
}
