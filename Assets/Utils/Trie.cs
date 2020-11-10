using System;
using System.Collections.Generic;
using Word = System.Collections.Generic.KeyValuePair<string, double>;

public class Trie {
    public char c;
    public Trie parent;
    public Dictionary<char, Trie> children;
    public List<LexiconItem> candidates;
    public int depth;

    public Trie(char c, Trie parent) {
        this.c = c;
        this.parent = parent;
        children = new Dictionary<char, Trie>();
        candidates = new List<LexiconItem>();
        depth = parent != null ? parent.depth + 1 : 0;
    }

    public virtual Trie GetNewNode(char c, Trie parent) {
        return new Trie(c, parent);
    }

    public void Build(LexiconItem item) {
        if (depth == item.inside.Length) {
            foreach (LexiconItem prev in candidates) {
                if (item.inside == prev.inside && item.outside == prev.outside) return;
            }
            candidates.Add(item);
            return;
        }
        char c = item.inside[depth];
        if (!children.ContainsKey(c)) children[c] = GetNewNode(c, this);
        children[c].Build(item);
    }
}

class ElasticTapPredictorTrie : Trie {
    public double[] dp;

    public ElasticTapPredictorTrie(char c, Trie parent, int MAX_WORD_LENGTH = 25) : base(c, parent) {
        dp = new double[MAX_WORD_LENGTH];
    }
    
    public override Trie GetNewNode(char c, Trie parent) {
        return new ElasticTapPredictorTrie(c, parent);
    }
}