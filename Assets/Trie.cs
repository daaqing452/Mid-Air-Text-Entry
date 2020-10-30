using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Trie {
    public char c;
    public Trie parent;
    public Dictionary<char, Trie> children;
    public bool isEndOfWord;
    public double logFreq;
    public string candidate;
    public int depth;
    public double[] dp;

    public Trie(char c, Trie parent) {
        this.c = c;
        this.parent = parent;
        children = new Dictionary<char, Trie>();
        isEndOfWord = false;
        logFreq = 0;
        candidate = "";
        depth = parent != null ? parent.depth + 1 : 0;
        dp = new double[25];
    }

    public virtual Trie GetNewNode(char c, Trie parent) {
        return new Trie(c, parent);
    }

    public void Build(string s, float value) {
        if (depth == s.Length) {
            isEndOfWord = true;
            logFreq = Math.Log(value);
            candidate = s;
            return;
        }
        char c = s[depth];
        if (!children.ContainsKey(c)) children[c] = GetNewNode(c, this);
        children[c].Build(s, value);
    }
}

class ElasticTapPredictorTrie : Trie {

    public ElasticTapPredictorTrie(char c, Trie parent, int MAX_WORD_LENGTH = 25) : base(c, parent) {
        dp = new double[MAX_WORD_LENGTH];
    }
    
    public override Trie GetNewNode(char c, Trie parent) {
        return new ElasticTapPredictorTrie(c, parent);
    }
}