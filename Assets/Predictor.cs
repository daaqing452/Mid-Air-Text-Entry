using System;
using System.Collections.Generic;
using UnityEngine;
using Word = System.Collections.Generic.KeyValuePair<string, double>;

public class Predictor {
    // constant after initialization
    protected const int MAX_WORD_LENGTH = 25;
    protected const int ALPHABET = 26;
    protected Keyboard keyboard;
    protected Vector2[] keys;
    public Lexicon lexicon;

    // readable variables
    public List<Word> candidateWords;
    public List<Vector2> inputs;

    public Predictor(Keyboard keyboard) {
        this.keyboard = keyboard;
        keys = new Vector2[26];
        foreach (GameObject key in keyboard.uKeyAnchors) {
            char c = key.name[4];
            keys[c - 'A'] = keyboard.Convert3DTo2DOnKeyboard(key.transform.position);
        }
        candidateWords = new List<Word>();
        inputs = new List<Vector2>();
    }

    ~Predictor() {
        Clear();
    }

    public virtual string Predict(Vector4 p, params object[] args) { return ""; }

    public virtual void Clear() {
        candidateWords.Clear();
        inputs.Clear();
    }

    public virtual void ReloadLexicon(Lexicon lexicon) {
        this.lexicon = lexicon;
    }

    protected void AddCandidateWordByAscending(Word newWord) {
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
    protected string literalText;

    public NaiveTapPredictor(Keyboard keyboard) : base(keyboard) { }

    public override string Predict(Vector4 p, params object[] args) {
        Vector2 p2D = keyboard.GetTouchPosition(p);
        UpdateLiteralText(p2D);
        return literalText;
    }

    public override void Clear() {
        base.Clear();
        literalText = "";
    }

    protected void UpdateLiteralText(Vector2 p2D) {
        char best = '_';
        float minDist = 1e20f;
        for (int i = 0; i < ALPHABET; i++) {
            Vector2 key = keys[i];
            if ((key - p2D).magnitude < minDist) {
                minDist = (key - p2D).magnitude;
                best = (char)(i + 'a');
            }
        }
        literalText += best;
    }
}

abstract class UniformBayesianTapPredictor : NaiveTapPredictor {
    public Vector2 SIGMA;

    Vector2 C0;
    Vector2 C1;
    Vector2 KEYBOARD_SIZE;

    public UniformBayesianTapPredictor(Keyboard keyboard) : base(keyboard) {
        SIGMA = new Vector2(0.5f / 10, 0.5f / 3);
        C0 = new Vector2((float)-Math.Log(Math.Sqrt(2.0 * Math.PI) * SIGMA.x), (float)-Math.Log(Math.Sqrt(2.0 * Math.PI) * SIGMA.y));
        C1 = new Vector2(1.0f / 2 / SIGMA.x / SIGMA.x, 1.0f / 2 / SIGMA.y / SIGMA.y);
        KEYBOARD_SIZE = new Vector2(keyboard.uKeyboardBase.transform.lossyScale.x, keyboard.uKeyboardBase.transform.lossyScale.y);
    }

    public double LogPUniformTouch(Vector2 touch2D, Vector2 c) {
        double tx = -Math.Pow((touch2D.x - c.x) / KEYBOARD_SIZE.x, 2) * C1.x;
        double ty = -Math.Pow((touch2D.y - c.y) / KEYBOARD_SIZE.y, 2) * C1.y;
        return C0.x + tx + C0.y + ty;
    }

    /*public double LogPUniformTouch(Vector2 touch2D, Vector2 c) {
        return LogP1DGaussian(touch2D.x, c.x, SIGMA.x) + LogP1DGaussianY(touch2D.y, c.y, SIGMA.y);
    }*/
    
    public double LogP1DGaussian(double x, double mu, double sigma) {
        return -Math.Log(Math.Sqrt(2.0 * Math.PI) * sigma) - Math.Pow(x - mu, 2) / 2 / sigma / sigma;
    }
}

class RigidTapPredictor : UniformBayesianTapPredictor {
    List<string>[] words;

    public RigidTapPredictor(Keyboard keyboard) : base(keyboard) { }
    
    public override string Predict(Vector4 p, params object[] args) {
        Vector2 p2D = keyboard.GetTouchPosition(p);
        inputs.Add(p2D);
        candidateWords.Clear();
        int n = inputs.Count;
        for (int i = 0; i < words[n].Count; i++) {
            string candidate = words[n][i];
            double logP = Math.Log(lexicon.unigram[candidate]);
            for (int j = 0; j < n; j++) {
                logP += LogPUniformTouch(inputs[j], keys[candidate[j] - 'a']);
            }
            AddCandidateWordByAscending(new Word(candidate, logP));
        }
        return candidateWords[0].Key;
    }

    public override void ReloadLexicon(Lexicon lexicon) {
        base.ReloadLexicon(lexicon);
        words = new List<string>[MAX_WORD_LENGTH];
        for (int i = 0; i < MAX_WORD_LENGTH; i++) words[i] = new List<string>();
        foreach (var item in lexicon.unigram) {
            string word = item.Key;
            words[word.Length].Add(word);
        }
    }
}

class BruteForceElasticTapPredictor : UniformBayesianTapPredictor {
    protected const int LENGTH_DIFF = 1;
    protected double LOG_INSERT_ERR;
    protected double LOG_OMIT_ERR;
    protected double LOG_SWAP_ERR;
    protected double MAX_LOGP_PER_TOUCH;

    public BruteForceElasticTapPredictor(Keyboard keyboard) : base(keyboard) {
        LOG_INSERT_ERR = Math.Log(0.01);
        LOG_OMIT_ERR = Math.Log(0.01);
        LOG_SWAP_ERR = Math.Log(0.004);
        MAX_LOGP_PER_TOUCH = LogP1DGaussian(0, 0, SIGMA.x) + LogP1DGaussian(0, 0, SIGMA.y);
    }

    public override string Predict(Vector4 p, params object[] args) {
        Vector2 p2D = keyboard.GetTouchPosition(p);
        inputs.Add(p2D);
        candidateWords.Clear();
        foreach (var item in lexicon.unigram) {
            string candidate = item.Key;
            // ignore too much difference in length
            if (Math.Abs(candidate.Length - inputs.Count) > LENGTH_DIFF) continue;
            // estimate logP
            double topLogP = (candidateWords.Count < Decoder.N_CANDIDATE) ? -1e20 : candidateWords[candidateWords.Count - 1].Value;
            double logP = Math.Log(item.Value);
            // elastic matching
            List<Vector2> standard = new List<Vector2>();
            for (int i = 0; i < candidate.Length; i++) standard.Add(keys[candidate[i] - 'a']);
            DateTime d2 = DateTime.Now;
            double logPElasticMatching = LogPElasticMatching(inputs, standard, topLogP - logP);
            logP += logPElasticMatching;
            // update candidate list
            AddCandidateWordByAscending(new Word(candidate, logP));
        }
        return candidateWords[0].Key;
    }
    
    double LogPElasticMatching(List<Vector2> a, List<Vector2> b, double minThres = -1e20, int K = 2) {
        int m = a.Count;
        int n = b.Count;
        double[,] dp = new double[m + 1, n + 1];
        for (int i = 0; i <= m; i++) {
            int jS = Math.Max(i - K, 0);
            int jT = Math.Min(i + K, n);
            double maxValue = -1e20;
            for (int j = jS; j <= jT; j++) {
                dp[i, j] = (i == 0 && j == 0) ? 0 : -1e20f;
                // match
                if (i > 0 && j > 0) dp[i, j] = Math.Max(dp[i, j], dp[i - 1, j - 1] + LogPUniformTouch(a[i - 1], b[j - 1]));
                // insertion error
                if (i > 0 && j - (i - 1) <= K) dp[i, j] = Math.Max(dp[i, j], dp[i - 1, j] + LOG_INSERT_ERR);
                // omission error
                if (j > 0 && i - (j - 1) <= K) dp[i, j] = Math.Max(dp[i, j], dp[i, j - 1] + LOG_OMIT_ERR);
                // swapping error
                if (i > 1 && j > 1) dp[i, j] = Math.Max(dp[i, j], dp[i - 2, j - 2] + LogPUniformTouch(a[i - 2], b[j - 1]) + LogPUniformTouch(a[i - 1], b[j - 2]));
                maxValue = Math.Max(maxValue, dp[i, j]);
            }
            // current + max_in_future <= minThres then exit
            if (maxValue + MAX_LOGP_PER_TOUCH * (m - i) < minThres) return -1e20;
        }
        return dp[m, n];
    }
}

class TrieElasticTapPredictor : BruteForceElasticTapPredictor {
    ElasticTapPredictorTrie root;
    
    public TrieElasticTapPredictor(Keyboard keyboard) : base(keyboard) { }
    
    public override string Predict(Vector4 p, params object[] args) {
        DateTime __d0 = DateTime.Now;
        Vector2 p2D = keyboard.GetTouchPosition(p);
        UpdateLiteralText(p2D);
        inputs.Add(p2D);
        candidateWords.Clear();
        RecursiveUpdate(root, inputs.Count);
        string top1Word = candidateWords[0].Key;
        candidateWords[0] = new Word(literalText, 0);
        if (keyboard.ShowPredictionTime) keyboard.InfoShow("time(tap): " + Math.Round((DateTime.Now - __d0).TotalMilliseconds, 3));
        return top1Word;
    }
    
    void RecursiveUpdate(ElasticTapPredictorTrie u, int n) {
        if (u.depth - n > LENGTH_DIFF) return;
        if (n - u.depth <= LENGTH_DIFF) {
            ElasticTapPredictorTrie p = (ElasticTapPredictorTrie)u.parent;
            ElasticTapPredictorTrie g = (p == null) ? null : (ElasticTapPredictorTrie)p.parent;
            u.dp[n] = (u == root && n == 0) ? 0 : -1e20f;
            // match
            if (p != null && n >= 1) u.dp[n] = Math.Max(u.dp[n], p.dp[n - 1] + LogPUniformTouch(inputs[n - 1], keys[u.c - 'a']));
            // insertion error
            if (n >= 1) u.dp[n] = Math.Max(u.dp[n], u.dp[n - 1] + LOG_INSERT_ERR);
            // omission error
            if (p != null) u.dp[n] = Math.Max(u.dp[n], p.dp[n] + LOG_OMIT_ERR);
            // swapping error
            if (g != null && n >= 2) u.dp[n] = Math.Max(u.dp[n], g.dp[n - 2] + LogPUniformTouch(inputs[n - 2], keys[u.c - 'a']) + LogPUniformTouch(inputs[n - 1], keys[p.c - 'a']) + LOG_SWAP_ERR);
            // update result
            foreach (LexiconItem item in u.candidates) {
                double logP = u.dp[n] + item.value;
                AddCandidateWordByAscending(new Word(item.outside, logP));
            }
        }
        foreach (var item in u.children) {
            RecursiveUpdate((ElasticTapPredictorTrie)item.Value, n);
        }
    }

    public override void ReloadLexicon(Lexicon lexicon) {
        base.ReloadLexicon(lexicon);
        // build trie, totally 50129 nodes
        root = new ElasticTapPredictorTrie(' ', null);
        foreach (var item in lexicon.unigram) {
            string candidate = item.Key;
            root.Build(new LexiconItem(candidate, Math.Log(item.Value), lexicon.inout[candidate]));
        }
        RecursiveUpdate(root, 0);
        candidateWords.Clear();
    }
}

class NaiveGesturePredictor : Predictor {
    protected const float EPS = 1e-3f;
    protected const double FREQ_WEIGHT = 0.005;
    protected const double E_MIN_DIST = 0.003;
    protected float keyboardScale;
    float[,] dpEM = new float[N_SAMPLE, N_SAMPLE];
    string nowWord;

    protected const int N_SAMPLE = 50;
    protected List<Vector2[]> standards;

    public NaiveGesturePredictor(Keyboard keyboard) : base(keyboard) {
        keyboardScale = keyboard.uKeyboardBase.transform.lossyScale.magnitude;
    }

    public override string Predict(Vector4 p, params object[] args) {
        Vector2 touch2D = keyboard.GetTouchPosition(p);
        int state = (int)args[0];
        if (state == (int)NaiveGestureExtractor.GestureInputState.Stay) {
            inputs.Add(touch2D);
        }
        if (state == (int)NaiveGestureExtractor.GestureInputState.Exit) {
            nowWord = EnumerateCandidates();
            return nowWord;
        }
        if (state == (int)NaiveGestureExtractor.GestureInputState.WaitForConfirm) {
            return nowWord;
        }
        return "";
    }
    
    public virtual string EnumerateCandidates() {
        DateTime __d0 = DateTime.Now;
        Vector2[] resampledInputs = Resample(inputs, N_SAMPLE);
        candidateWords.Clear();
        int j = -1;
        foreach (var item in lexicon.unigram) {
            j++;
            string candidate = item.Key;
            double topScore = (candidateWords.Count < Decoder.N_CANDIDATE) ? -1e20 : candidateWords[candidateWords.Count - 1].Value;
            double score = Math.Log(item.Value) * FREQ_WEIGHT;
            if (score - E_MIN_DIST < topScore) continue;
            float dist = ElasticMatching(resampledInputs, standards[j], (float)(score - topScore));
            score -= dist;
            AddCandidateWordByAscending(new Word(candidate, score));
        }
        if (keyboard.ShowPredictionTime) keyboard.InfoShow("time(gesture): " + Math.Round((DateTime.Now - __d0).TotalMilliseconds, 3));
        return candidateWords[0].Key;
    }

    protected Vector2[] Resample(List<Vector2> a, int nSample) {
        // calculate stride
        float length = 0;
        for (int i = 1; i < a.Count; i++) length += (a[i] - a[i - 1]).magnitude;
        float stride = length / (nSample - 1);
        // gesture with only one position
        List<Vector2> b = new List<Vector2>();
        if (length < EPS) {
            for (int i = 0; i < nSample; i++) b.Add(a[0]);
            return b.ToArray();
        }
        // resample normal gesture
        b.Add(a[0]);
        float currentLength = 0;
        for (int i = 1, j = 1; i < nSample; i++) {
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
        return b.ToArray();
    }

    protected float ElasticMatching(Vector2[] a, Vector2[] b, float maxThres = 1e20f, int K = 5) {
        int n = a.Length;
        for (int i = 0; i < n; i++) {
            int jS = Math.Max(i - K, 0);
            int jT = Math.Min(i + K, n - 1);
            float minValue = 1e20f;
            for (int j = jS; j <= jT; j++) {
                dpEM[i, j] = (i == 0 && j == 0) ? 0 : 1e20f;
                if (i > 0 && j - (i - 1) <= K) dpEM[i, j] = Math.Min(dpEM[i, j], dpEM[i - 1, j]);
                if (j > 0 && i - (j - 1) <= K) dpEM[i, j] = Math.Min(dpEM[i, j], dpEM[i, j - 1]);
                if (i > 0 && j > 0) dpEM[i, j] = Math.Min(dpEM[i, j], dpEM[i - 1, j - 1]);
                dpEM[i, j] += (a[i] - b[j]).sqrMagnitude;
                minValue = Math.Min(minValue, dpEM[i, j]);
            }
            if (minValue / keyboardScale > maxThres) return 1e20f;
        }
        return dpEM[n - 1, n - 1] / keyboardScale;
    }

    protected float RigidMatching(Vector2[] a, Vector2[] b) {
        int n = a.Length;
        float dist = 0;
        for (int i = 0; i < n; i++) {
            dist += (a[i] - b[i]).sqrMagnitude;
        }
        return dist / keyboardScale;
    }

    protected float RigidMatching(Vector2[] a, Vector2[] b, float maxThres = 1e20f) {
        int n = a.Length;
        float dist = 0;
        for (int i = 0; i < n; i++) {
            dist += (a[i] - b[i]).sqrMagnitude;
            if (dist > maxThres) return 1e20f;
        }
        return dist / keyboardScale;
    }

    public override void ReloadLexicon(Lexicon lexicon) {
        base.ReloadLexicon(lexicon);
        standards = new List<Vector2[]>();
        foreach (var item in lexicon.unigram) {
            string candidate = item.Key;
            List<Vector2> wordPoints = new List<Vector2>();
            for (int i = 0; i < candidate.Length; i++) wordPoints.Add(keys[candidate[i] - 'a']);
            standards.Add(Resample(wordPoints, N_SAMPLE));
        }
    }
}

class TwoLevelGesturePredictor : NaiveGesturePredictor {
    class FirstLevelItem : IComparable<FirstLevelItem> {
        public string candidate;
        public double freq;
        public int index;
        public double value;

        public FirstLevelItem(string candidate, double freq, int index, double value) {
            this.candidate = candidate;
            this.freq = freq;
            this.index = index;
            this.value = value;
        }

        public int CompareTo(FirstLevelItem obj) {
            return value - obj.value > 0 ? 1 : -1;
        }
    }

    const int N_SAMPLE_L1 = 10;
    const int N_CANDIDATE_L1 = 100;
    List<Vector2[]> standardsL1;

    public TwoLevelGesturePredictor(Keyboard keyboard) : base(keyboard) { }

    public override string EnumerateCandidates() {
        DateTime __d0 = DateTime.Now;
        // first level
        Vector2[] resampledInputsL1 = Resample(inputs, N_SAMPLE_L1);
        int j = -1;
        List<FirstLevelItem> items = new List<FirstLevelItem>();
        foreach (var item in lexicon.unigram) {
            j++;
            float dist = RigidMatching(resampledInputsL1, standardsL1[j]);
            items.Add(new FirstLevelItem(item.Key, item.Value, j, dist));
        }
        GetTopCandidatesByMergeSort(items, 0, items.Count);
        // second level
        Vector2[] resampledInputs = Resample(inputs, N_SAMPLE);
        candidateWords.Clear();
        for (int i = 0; i < N_CANDIDATE_L1; i++) {
            FirstLevelItem item = items[i];
            string candidate = item.candidate;
            double topScore = (candidateWords.Count < Decoder.N_CANDIDATE) ? -1e20 : candidateWords[candidateWords.Count - 1].Value;
            double score = Math.Log(item.freq) * FREQ_WEIGHT;
            if (score - E_MIN_DIST < topScore) continue;
            float dist = ElasticMatching(resampledInputs, standards[item.index], (float)(score - topScore));
            score -= dist;
            AddCandidateWordByAscending(new Word(lexicon.inout[candidate], score));
        }
        DateTime __d3 = DateTime.Now;
        if (keyboard.ShowPredictionTime) keyboard.InfoShow("time(gesture): " + Math.Round((__d3 - __d0).TotalMilliseconds, 3));
        return candidateWords[0].Key;
    }
    
    void GetTopCandidatesByMergeSort(List<FirstLevelItem> items, int l, int r, int d = 0) {
        if (!(l < N_CANDIDATE_L1 / 2 && r > N_CANDIDATE_L1)) return;
        FirstLevelItem anchor = items[l];
        FirstLevelItem[] tmp = new FirstLevelItem[r - l];
        int ml = l, mr = r;
        for (int i = l + 1; i < r; i++) {
            if (items[i].value < anchor.value) tmp[(ml++) - l] = items[i]; else tmp[(--mr) - l] = items[i];
        }
        tmp[ml - l] = anchor;
        for (int i = l; i < r; i++) items[i] = tmp[i - l];
        GetTopCandidatesByMergeSort(items, l, ml, d + 1);
        GetTopCandidatesByMergeSort(items, mr, r, d + 1);
    }

    public override void ReloadLexicon(Lexicon lexicon) {
        base.ReloadLexicon(lexicon);
        standardsL1 = new List<Vector2[]>();
        foreach (var item in lexicon.unigram) {
            string candidate = item.Key;
            List<Vector2> wordPoints = new List<Vector2>();
            for (int i = 0; i < candidate.Length; i++) wordPoints.Add(keys[candidate[i] - 'a']);
            standardsL1.Add(Resample(wordPoints, N_SAMPLE_L1));
        }
    }
}