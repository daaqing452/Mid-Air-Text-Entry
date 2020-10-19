using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Extractor
{
    public Decoder decoder;

    // output
    public Vector4 target;

    public Extractor(Decoder decoder) {
        this.decoder = decoder;
    }

    public virtual bool Input(Vector4 nowP) { return false; }
}

class WhiteBoxExtractor : Extractor {
    // thresholds
    const float DD_THRES = 0.003f;
    const float MIND_NOISE = 0.005f;

    // temporal varaible
    List<Vector4> p;
    List<float> dd;
    int l, r;
    
    public WhiteBoxExtractor(Decoder decoder) : base(decoder) {
        p = new List<Vector4>();
        p.Add(new Vector4(0, 0, 0, decoder.keyboard.TypeZoneDist));
        dd = new List<float>();
        l = r = -1;
    }

    public override bool Input(Vector4 nowP) {
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
            return true;
        }
        return false;
    }

    void GetTarget(int ll, int rr) {
        float minD = 1e20f;
        for (int i = ll; i <= rr; i++) minD = Math.Min(minD, p[i].w);
        int j;
        for (j = rr; j >= ll; j--) if (p[j].w < minD + MIND_NOISE) break;
        target = p[j];
    }

    void RenewStatus(int c) {
        //p.RemoveRange(0, c);
        //dd.RemoveRange(0, c);
        l = -1;
        r = -1;
    }
}