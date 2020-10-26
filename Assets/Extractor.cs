using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Extractor
{
    // output
    public Vector4 target;

    public Extractor() { }

    ~Extractor() {
        Clear();
    }

    public virtual int Input(Vector4 p, params object[] args) { return 0; }

    public virtual void Clear() { }
}

class WhiteBoxDepthExtractor : Extractor {
    // thresholds
    const float TYPE_ZONE_HEIGHT = 0.03f;
    const float DD_THRES = 0.003f;
    const float MIND_NOISE = 0.005f;

    // temporal varaible
    List<Vector4> p;
    List<float> dd;
    int l, r;
    
    public WhiteBoxDepthExtractor() : base() {
        p = new List<Vector4>();
        dd = new List<float>();
        Clear();
    }

    public override int Input(Vector4 nowP, params object[] args) {
        bool inTypeZone = (bool)args[1];
        if (!inTypeZone) nowP.w = TYPE_ZONE_HEIGHT;
        float nowDD = nowP.w - p[p.Count - 1].w;
        p.Add(nowP);
        dd.Add(nowDD);
        // tap down
        if (nowDD < -DD_THRES) {
            // tap down when successive tap
            /*if (r != -1) {
                if (l != -1) {
                    GetTarget(l, r);
                }
                RenewStatus(r - 1);
            }*/
            if (l == -1) {
                l = dd.Count - 1;
            }
        }
        // lift up before successive tap
        /*if (nowDD > 0) {
            r = dd.Count - 1;
        }*/
        // lift up
        if (nowDD > DD_THRES && l > -1) {
            GetTarget(l, dd.Count);
            RenewStatus(dd.Count - 1);
            return 1;
        }
        return 0;
    }

    void GetTarget(int ll, int rr) {
        float minD = 1e20f;
        for (int i = ll; i <= rr; i++) minD = Math.Min(minD, p[i].w);
        int j;
        for (j = rr; j >= ll; j--) if (p[j].w < minD + MIND_NOISE) break;
        target = p[j];
    }

    void RenewStatus(int deleteBound) {
        p.RemoveRange(0, deleteBound);
        dd.RemoveRange(0, deleteBound);
        l = r = -1;
    }

    public override void Clear() {
        p.Clear();
        p.Add(new Vector4(0, 0, 0, TYPE_ZONE_HEIGHT));
        dd.Clear();
        l = r = -1;
    }
}

class NaiveGestureExtractor : Extractor {
    public enum GestureInputState { None, Enter, Stay, Exit, WaitForConfirm };
    GestureInputState state;
    bool gestureTyping = false;

    public NaiveGestureExtractor() : base() {
        state = GestureInputState.None;
    }

    public override int Input(Vector4 p, params object[] args) {
        bool ifTouchKeyboard = (bool)args[0];
        bool ifInTypeZone = (bool)args[1];
        if (state == GestureInputState.WaitForConfirm || state == GestureInputState.Exit) {
            // wait for confirm
            state = GestureInputState.WaitForConfirm;
            return (int)state;
        }
        // enter
        if (!gestureTyping && ifTouchKeyboard) {
            gestureTyping = true;
            state = GestureInputState.Enter;
            return (int)state;
        }
        // exit
        if (gestureTyping && !ifInTypeZone) {
            gestureTyping = false;
            state = GestureInputState.Exit;
            return (int)state;
        }
        state = gestureTyping ? GestureInputState.Stay : GestureInputState.None;
        return (int)state;
    }

    public override void Clear() {
        base.Clear();
        state = GestureInputState.None;
    }
}