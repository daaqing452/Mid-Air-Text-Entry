using System;
using System.Collections.Generic;
using UnityEngine;
using Word = System.Collections.Generic.KeyValuePair<string, float>;

public class Predictor {
    public Decoder decoder;
    public Keyboard keyboard;
    public List<Word> candidateWords;
    public Dictionary<string, int> freq = new Dictionary<string, int>();

    public Predictor(Decoder decoder) {
        this.decoder = decoder;
        keyboard = decoder.keyboard;
        candidateWords = new List<Word>();

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
    }

    public Vector2 HybridTouchPosition(Vector4 p, Vector2 lastTouchOnKeyboard2D) {
        Vector2 ans = new Vector3(0, 0);
        if (p.w > 0) {
            Vector3 finger = new Vector3(p.x, p.y, p.z);
            Vector3 touch = keyboard.SeeThroughPointProjectOnKeyboard(finger);
            ans = keyboard.Point3DTo2DOnKeyboard(touch);
        } else {
            ans = lastTouchOnKeyboard2D;
        }
        return ans;
    }

    public void AddCandidateWordsByAscending(Word newWord) {
        candidateWords.Add(newWord);
        int k = candidateWords.Count - 1;
        while (k > 0) {
            if (candidateWords[k].Value >= candidateWords[k - 1].Value) break;
            var temp = candidateWords[k];
            candidateWords[k] = candidateWords[k - 1];
            candidateWords[k - 1] = temp;
            k--;
        }
        if (candidateWords.Count > Decoder.N_CANDIDATE) candidateWords.RemoveAt(candidateWords.Count - 1);
    }
}

class NaiveTapPredictor : Predictor {
     
    public NaiveTapPredictor(Decoder decoder) : base(decoder) {
    }

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
        return decoder.nowWord + best;
    }
}

class RigidBayesianPredictor : Predictor {
    const int MAX_WORD_LENGTH = 25;
    List<string>[] words;
    Vector2[] keys;
    List<Vector2> inputs;

    public RigidBayesianPredictor(Decoder decoder) : base(decoder) {
        words = new List<string>[MAX_WORD_LENGTH];
        for (int i = 0; i < MAX_WORD_LENGTH; i++) words[i] = new List<string>();
        foreach (var item in freq) {
            string word = item.Key;
            words[word.Length].Add(word);
        }
        keys = new Vector2[26];
        foreach (GameObject key in keyboard.keyAnchors) {
            char c = key.name[4];
            keys[c - 'A'] = keyboard.Point3DTo2DOnKeyboard(key.transform.position);
        }
        inputs = new List<Vector2>();
    }

    public override string Predict(Vector4 p, params object[] args) {
        // get input
        Vector2 now = HybridTouchPosition(p, (Vector2)args[0]);
        inputs.Add(now);
        // enumerate candidate words
        int n = inputs.Count;
        candidateWords.Clear();
        for (int i = 0; i < words[n].Count; i++) {
            string candidate = words[n][i];
            // get diff
            float diff = 0;
            for (int j = 0; j < n; j++) {
                float d = (inputs[j] - keys[candidate[j] - 'a']).magnitude;
                diff += d * d;
            }
            AddCandidateWordsByAscending(new Word(candidate, diff));
        }
        return candidateWords[0].Key;
    }

    public override void Clear() {
        base.Clear();
        inputs.Clear();
    }
}

class NaiveGesturePredictor : Predictor {
    const float EPS = 1e-3f;
    const int   N_SAMPLE = 50;
    const float ELASTIC_MATCHING_COEFF = 1;
    Vector2[] keys;
    List<Vector2> inputs;

    public NaiveGesturePredictor(Decoder decoder) : base(decoder) {
        keys = new Vector2[26];
        foreach (GameObject key in keyboard.keyAnchors) {
            char c = key.name[4];
            keys[c - 'A'] = keyboard.Point3DTo2DOnKeyboard(key.transform.position);
        }
        inputs = new List<Vector2>();
    }

    public override string Predict(Vector4 p, params object[] args) {
        Vector2 now = keyboard.PointProjectOnKeyboard(new Vector3(p.x, p.y, p.z));
        NaiveGestureExtractor.GestureInputState state = (NaiveGestureExtractor.GestureInputState)args[0];
        if (state == NaiveGestureExtractor.GestureInputState.Stay) {
            inputs.Add(now);
        }
        if (state == NaiveGestureExtractor.GestureInputState.Exit) {
            List<Vector2> resampledInputs = Resample(inputs);
            candidateWords.Clear();
            float minDist = 1e20f;
            string bestString = "";
            foreach (var item in freq) {
                string candidate = item.Key;
                List<Vector2> standard = GetStandardPattern(candidate);
                float dist = ElasticMatching(resampledInputs, standard);
                float score = -(float)Math.Log(item.Value) + ELASTIC_MATCHING_COEFF * dist;
                AddCandidateWordsByAscending(new Word(candidate, score));
                if (dist < minDist) {
                    minDist = dist;
                    bestString = candidate;
                }
            }
            string res = candidateWords[0].Key;
            keyboard.ShowInfo(bestString + " " + Math.Round(minDist, 5));
            return res;
        }
        return "";
    }
    
    public override void Clear() {
        base.Clear();
        inputs.Clear();
    }

    public List<Vector2> GetStandardPattern(string word) {
        List<Vector2> wordPoints = new List<Vector2>();
        for (int i = 0; i < word.Length; i++) wordPoints.Add(keys[word[i] - 'A']);
        return Resample(wordPoints);
    }

    public List<Vector2> Resample(List<Vector2> a) {
        // calculate stride
        float length = 0;
        for (int i = 1; i < a.Count; i++) length += (a[i] - a[i - 1]).magnitude;
        float stride = length / (N_SAMPLE - 1);
        // resample
        List<Vector2> b = new List<Vector2>();
        b.Add(a[0]);
        float currentLength = 0;
        for (int i = 1, j = 1; i < N_SAMPLE; i++) {
            float targetLength = i * stride;
            float currentSpan = 0;
            while (j < a.Count) {
                currentSpan = (a[j] - a[j - 1]).magnitude;
                if (currentLength + currentSpan > targetLength - EPS) break;
                j++;
            }
            Vector2 now = Vector2.Lerp(a[j - 1], a[j], (targetLength - currentLength) / currentSpan);
            b.Add(now);
        }
        return b;
    }

    public float ElasticMatching(List<Vector2> a, List<Vector2> b) {
        float[,] f = new float[N_SAMPLE + 1, N_SAMPLE + 1];
        for (int i = 0; i <= N_SAMPLE; i++)
            for (int j = 0; j <= N_SAMPLE; j++) {
                if (i == 0 && j == 0) {
                    f[i, j] = 0;
                    continue;
                }
                if (i > 0) {
                    f[i, j] = Math.Min(f[i, j], f[i - 1, j]);
                }
                if (j > 0) {
                    f[i, j] = Math.Min(f[i, j], f[i, j - 1]);
                }
                if (i > 0 && j > 0) {
                    f[i, j] = Math.Min(f[i, j], f[i - 1, j - 1] + (a[i - 1] - b[j - 1]).magnitude);
                }
            }
        return f[N_SAMPLE, N_SAMPLE];
    }
}