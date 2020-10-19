using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class XFileManager : MonoBehaviour
{
    void Start() { }

    // Update is called once per frame
    void Update() { }

    public static string[] ReadLines(string filename) {
        if (Application.platform == RuntimePlatform.WindowsEditor) {
            StreamReader reader = new StreamReader(new FileStream(Application.streamingAssetsPath + "/" + filename, FileMode.Open));
            List<string> lines = new List<string>();
            while (true) {
                string line = reader.ReadLine();
                if (line == null) break;
                lines.Add(line);
            }
            reader.Close();
            return lines.ToArray();
        } else if (Application.platform == RuntimePlatform.Android) {
            string url = Application.streamingAssetsPath + "/" + filename;
            WWW www = new WWW(url);
            while (!www.isDone) { }
            return www.text.Split('\n');
        }
        return new string[0];
    }

    public static void WriteLine(string fileName, string s, bool append = true) {
        StreamWriter writer;
        FileMode fileMode = append ? FileMode.Append : FileMode.Create;
        if (Application.platform == RuntimePlatform.WindowsEditor) {
            writer = new StreamWriter(new FileStream(Application.dataPath + "/" + fileName, fileMode));
        } else if (Application.platform == RuntimePlatform.Android) {
            writer = new StreamWriter(new FileStream(Application.persistentDataPath + "/" + fileName, fileMode));
        } else {
            writer = null;
        }
        long nowTime = DateTime.Now.ToFileTimeUtc() / 10000 % 100000000;
        writer.WriteLine(nowTime + " " + s);
        writer.Flush();
        writer.Close();
    }
}
