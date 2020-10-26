using System;
using System.Collections.Generic;
using UnityEngine;
using Word = System.Collections.Generic.KeyValuePair<string, double>;

public class Predictor {
    public Keyboard keyboard;
    public Vector2[] keys;
    public List<Word> candidateWords;
    public List<Vector2> inputs;
    public Dictionary<string, int> freq = new Dictionary<string, int>();

    public Predictor(Keyboard keyboard) {
        this.keyboard = keyboard;
        keys = new Vector2[26];
        foreach (GameObject key in keyboard.keyAnchors) {
            char c = key.name[4];
            keys[c - 'A'] = keyboard.Convert3DTo2DOnKeyboard(key.transform.position);
        }
        candidateWords = new List<Word>();
        inputs = new List<Vector2>();

        // load lexicon files
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

    ~Predictor() {
        Clear();
    }

    public virtual string Predict(Vector4 p, params object[] args) { return ""; }

    public virtual void Clear() {
        candidateWords.Clear();
        inputs.Clear();
    }

    public Vector2 HybridTouchPosition(Vector4 p, Vector2 lastTouchOnKeyboard2D) {
        Vector2 touch2D = new Vector3(0, 0);
        if (p.w > 0) {
            Vector3 finger = new Vector3(p.x, p.y, p.z);
            Vector3 touch = keyboard.SeeThroughPointProjectOnKeyboard(finger);
            touch2D = keyboard.Convert3DTo2DOnKeyboard(touch);
        } else {
            touch2D = lastTouchOnKeyboard2D;
        }
        return touch2D;
    }

    public void AddCandidateWordsByAscending(Word newWord) {
        candidateWords.Add(newWord);
        int k = candidateWords.Count - 1;
        while (k > 0) {
            if (candidateWords[k].Value <= candidateWords[k - 1].Value) break;
            var temp = candidateWords[k];
            candidateWords[k] = candidateWords[k - 1];
            candidateWords[k - 1] = temp;
            k--;
        }
        if (candidateWords.Count > Decoder.N_CANDIDATE) candidateWords.RemoveAt(candidateWords.Count - 1);
    }
}

class NaiveTapPredictor : Predictor {
     
    public NaiveTapPredictor(Keyboard keyboard) : base(keyboard) { }

    public override string Predict(Vector4 p, params object[] args) {
        Vector3 p2 = new Vector3(p.x, p.y, p.z);
        char best = '_';
        float minDist = 1e20f;
        foreach (GameObject key in keyboard.keyAnchors) {
            Vector3 k = key.transform.position;
            if ((k - p2).magnitude < minDist) {
                minDist = (k - p2).magnitude;
                best = (char)(key.name[4] - 'A' + 'a');
            }
        }
        return "" + best;
    }
}

class RigidBayesianPredictor : Predictor {
    const int MAX_WORD_LENGTH = 25;
    float SIGMA_X;
    float SIGMA_Y;
    List<string>[] words;

    public RigidBayesianPredictor(Keyboard keyboard) : base(keyboard) {
        words = new List<string>[MAX_WORD_LENGTH];
        for (int i = 0; i < MAX_WORD_LENGTH; i++) words[i] = new List<string>();
        foreach (var item in freq) {
            string word = item.Key;
            words[word.Length].Add(word);
        }
        SIGMA_X = keyboard.keyboardBase.transform.lossyScale.x / 10 / 2;
        SIGMA_Y = keyboard.keyboardBase.transform.lossyScale.y / 3 / 2;
    }
    
    public override string Predict(Vector4 p, params object[] args) {
        // get input
        //Vector2 now = HybridTouchPosition(p, (Vector2)args[0]);
        Vector3 finger = new Vector3(p.x, p.y, p.z);
        Vector3 touch = keyboard.SeeThroughPointProjectOnKeyboard(finger);
        Vector2 touch2D = keyboard.Convert3DTo2DOnKeyboard(touch);
        inputs.Add(touch2D);
        // enumerate candidate words
        int n = inputs.Count;
        candidateWords.Clear();
        for (int i = 0; i < words[n].Count; i++) {
            string candidate = words[n][i];
            double logP = Math.Log(freq[candidate]);
            for (int j = 0; j < n; j++) {
                logP += LogRigidTouchModelP(inputs[j], keys[candidate[j] - 'a']);
            }
            AddCandidateWordsByAscending(new Word(candidate, logP));
        }
        return candidateWords[0].Key;
    }

    public double LogRigidTouchModelP(Vector2 touch2D, Vector2 c) {
        return Log1DGaussianP(touch2D.x, c.x, SIGMA_X) + Log1DGaussianP(touch2D.y, c.y, SIGMA_Y);
    }

    public double Log1DGaussianP(float x, float mu, float sigma) {
        return - Math.Log(Math.Sqrt(2.0 * Math.PI) * sigma) - Math.Pow(x - mu, 2) / 2 / sigma / sigma;
    }
}

class NaiveGesturePredictor : Predictor {
    const float EPS = 1e-3f;
    const int   N_SAMPLE = 50;
    const float ELASTIC_MATCHING_COEFF = 1;

    public NaiveGesturePredictor(Keyboard keyboard) : base(keyboard) { }

    public override string Predict(Vector4 p, params object[] args) {
        Vector3 touch = keyboard.PointProjectOnKeyboard(new Vector3(p.x, p.y, p.z));
        Vector2 touch2D = keyboard.Convert3DTo2DOnKeyboard(touch);
        NaiveGestureExtractor.GestureInputState state = (NaiveGestureExtractor.GestureInputState)args[0];
        if (state == NaiveGestureExtractor.GestureInputState.Stay) {
            inputs.Add(touch2D);
        }
        if (state == NaiveGestureExtractor.GestureInputState.Exit) {
            List<Vector2> resampledInputs = Resample(inputs);
            candidateWords.Clear();
            int cnt = 0;
            float scoreHello = 0;
            foreach (var item in freq) {
                string candidate = item.Key;
                List<Vector2> standard = GetStandardPattern(candidate);
                float dist = ElasticMatching(resampledInputs, standard);
                //float score = -(float)Math.Log(item.Value) + ELASTIC_MATCHING_COEFF * dist;
                float score = -ELASTIC_MATCHING_COEFF * dist;
                AddCandidateWordsByAscending(new Word(candidate, score));
                if (++cnt > 1000) break;
                if (candidate == "the") {
                    scoreHello = score;
                }
            }
            keyboard.ShowInfo(scoreHello + " " + candidateWords[0].Value);
            return candidateWords[0].Key;
        }
        return "";
    }

    public List<Vector2> GetStandardPattern(string word) {
        List<Vector2> wordPoints = new List<Vector2>();
        for (int i = 0; i < word.Length; i++) wordPoints.Add(keys[word[i] - 'a']);
        return Resample(wordPoints);
    }

    public List<Vector2> Resample(List<Vector2> a) {
        // calculate stride
        float length = 0;
        for (int i = 1; i < a.Count; i++) length += (a[i] - a[i - 1]).magnitude;
        float stride = length / (N_SAMPLE - 1);
        // gesture with only one position
        List<Vector2> b = new List<Vector2>();
        if (length < EPS) {
            for (int i = 0; i < N_SAMPLE; i++) b.Add(a[0]);
            return b;
        }
        // resample normal gesture
        b.Add(a[0]);
        float currentLength = 0;
        for (int i = 1, j = 1; i < N_SAMPLE; i++) {
            float targetLength = i * stride;
            float currentSpan = 0;
            while (j < a.Count) {
                currentSpan = (a[j] - a[j - 1]).magnitude;
                if (currentLength + currentSpan > targetLength - EPS) break;
                currentLength += currentSpan;
                j++;
            }
            Vector2 now = Vector2.Lerp(a[j - 1], a[j], (targetLength - currentLength) / currentSpan);
            b.Add(now);
        }
        return b;
    }

    public float ElasticMatching(List<Vector2> a, List<Vector2> b) {
        float[,] f = new float[N_SAMPLE, N_SAMPLE];
        for (int i = 0; i < N_SAMPLE; i++)
            for (int j = 0; j < N_SAMPLE; j++) {
                f[i, j] = (i == 0 && j == 0) ? 0 : 1e20f;
                if (i > 0)          f[i, j] = Math.Min(f[i, j], f[i - 1, j]);
                if (j > 0)          f[i, j] = Math.Min(f[i, j], f[i, j - 1]);
                if (i > 0 && j > 0) f[i, j] = Math.Min(f[i, j], f[i - 1, j - 1]);
                f[i, j] += (a[i] - b[j]).magnitude;
            }
        return f[N_SAMPLE - 1, N_SAMPLE - 1];
    }
}