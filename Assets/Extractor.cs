using System;
using System.Collections.Generic;
using UnityEngine;

public class Extractor {
    protected Keyboard keyboard;

    public Extractor(Keyboard keyboard) {
        this.keyboard = keyboard;
    }

    ~Extractor() {
        Clear();
    }

    public virtual int Input(Vector4 p, params object[] args) { return 0; }

    public virtual void Clear() { }
}

class WhiteBoxDepthTapExtractor : Extractor {
    public enum TapInputState { Out, InTypeZone, TouchKeyboard, TapDown, LiftUp };
    const float TYPE_ZONE_HEIGHT = 0.03f;
    const float DD_THRES = 0.003f;
    const float MIND_NOISE = 0.005f;

    // tap detect
    List<Vector4> ps;
    List<float> dd;
    int l, r;
    float minD;
    public Vector4 target;

    public WhiteBoxDepthTapExtractor(Keyboard keyboard) : base(keyboard) {
        ps = new List<Vector4>();
        dd = new List<float>();
        Clear();
    }
    
    public override int Input(Vector4 p, params object[] args) {
        Touch touch = (Touch)args[0];
        TapInputState currentFrameState = TapInputState.InTypeZone;
        // out
        if (!touch.IfInTypeZone()) {
            p.w = TYPE_ZONE_HEIGHT;
            ClearRange(dd.Count);
            currentFrameState = TapInputState.Out;
        }
        // detect touch keyboard
        if (touch.IfTouchKeyboard()) {
            currentFrameState = TapInputState.TouchKeyboard;
        }
        // update target
        if (l > -1) {
            minD = Math.Min(minD, p.w);
            minD = Math.Max(minD, -0.01f);
            if (p.w < minD + MIND_NOISE) target = p;
        }
        // tap detect
        float nowDD = p.w - ps[ps.Count - 1].w;
        ps.Add(p);
        dd.Add(nowDD);
        // tap down
        if (nowDD < -DD_THRES && ps.Count >= 2 && ps[ps.Count - 2].w < TYPE_ZONE_HEIGHT) {
            // tap down when successive tap
            /*if (r != -1) {
                if (l != -1) {
                    GetTarget(l, r);
                }
                RenewStatus(r - 1);
            }*/
            if (l == -1) {
                l = dd.Count - 1;
                minD = 1e20f;
                currentFrameState = TapInputState.TapDown;
            }
        }
        // lift up before successive tap
        /*if (nowDD > 0) {
            r = dd.Count - 1;
        }*/
        // lift up
        if (nowDD > DD_THRES && l > -1) {
            ClearRange(dd.Count - 1);
            currentFrameState = TapInputState.LiftUp;
        }
        return (int)currentFrameState;
    }

    void ClearRange(int upperbound) {
        ps.RemoveRange(0, upperbound);
        dd.RemoveRange(0, upperbound);
        l = r = -1;
    }

    public override void Clear() {
        ps.Clear();
        ps.Add(new Vector4(0, 0, 0, TYPE_ZONE_HEIGHT));
        dd.Clear();
        l = r = -1;
    }
}

class NaiveGestureExtractor : Extractor {
    public enum GestureInputState { None, Enter, Stay, Exit, WaitForConfirm };
    public int state;
    bool gestureTyping = false;

    public NaiveGestureExtractor(Keyboard keyboard) : base(keyboard) {
        state = (int)GestureInputState.None;
    }

    public override int Input(Vector4 p, params object[] args) {
        Touch touch = (Touch)args[0];
        if (state == (int)GestureInputState.WaitForConfirm || state == (int)GestureInputState.Exit) {
            // wait for confirm
            state = (int)GestureInputState.WaitForConfirm;
            return (int)state;
        }
        // enter
        if (!gestureTyping && touch.IfTouchKeyboard()) {
            gestureTyping = true;
            state = (int)GestureInputState.Enter;
            return state;
        }
        // exit
        if (gestureTyping && !touch.IfInTypeZone()) {
            gestureTyping = false;
            state = (int)GestureInputState.Exit;
            return state;
        }
        state = gestureTyping ? (int)GestureInputState.Stay : (int)GestureInputState.None;
        return state;
    }

    public override void Clear() {
        base.Clear();
        state = (int)GestureInputState.None;
    }
}