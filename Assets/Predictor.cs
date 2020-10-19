using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Predictor
{
    public Decoder decoder;

    // keyboard model: possibilities
    Dictionary<char, float> P_Keys;
    float P_Click;

    public Predictor(Decoder decoder) {
        this.decoder = decoder;
        P_Keys = new Dictionary<char, float>();
        P_Click = 1;
    }

    public virtual void Predict(Vector4 p) { }
}

class NaivePredictor : Predictor {
    Keyboard keyboard;
     
    public NaivePredictor(Decoder decoder) : base(decoder) {
        keyboard = decoder.keyboard;
    }

    public override void Predict(Vector4 p) {
        Vector3 p2 = new Vector3(p.x, p.y, p.z);
        char best = '_';
        float minDist = 1e20f;
        foreach (GameObject key in keyboard.keyAnchors) {
            Vector3 k = key.transform.position;
            if ((k - p2).magnitude < minDist) {
                minDist = (k - p2).magnitude;
                best = key.name[4];
            }
        }
        keyboard.outputText.text = keyboard.outputText.text + best;
    }
}