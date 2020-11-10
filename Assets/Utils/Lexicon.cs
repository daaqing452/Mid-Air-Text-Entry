using System;
using System.Collections.Generic;

public class Lexicon {
    public Dictionary<string, double> unigram;
    public Dictionary<string, string> inout;

    public Lexicon() {
        unigram = new Dictionary<string, double>();
        inout = new Dictionary<string, string>();
    }

    public virtual void AddUnigram(string word, double value, string wordForOutput = null) {
        unigram[word] = value;
        if (wordForOutput == null) wordForOutput = word;
        inout[word] = wordForOutput;
    }
}

public class LexiconItem {
    public string inside;
    public double value;
    public string outside;

    public LexiconItem(string inside, double value, string outside = null) {
        this.inside = inside;
        this.value = value;
        this.outside = outside == null ? inside : outside;
    }
}