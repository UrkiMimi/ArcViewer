using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class FileUtil
{
    public static AudioType GetAudioTypeByDirectory(string directory)
    {
        AudioType type = AudioType.UNKNOWN;
        string check = directory.ToLower();
        if(directory.EndsWith(".ogg") || directory.EndsWith(".egg"))
        {
            type = AudioType.OGGVORBIS;
        }
        else if(directory.EndsWith(".wav"))
        {
            type = AudioType.WAV;
        }
        else if(directory.EndsWith(".mp3"))
        {
            type = AudioType.MPEG;
        }

        return type;
    }
}


public sealed class TempFile : IDisposable
{
    private string path;
    public TempFile() : this(System.IO.Path.GetTempFileName()) { }

    public TempFile(string path)
    {
        if (string.IsNullOrEmpty(path)) throw new ArgumentNullException("path");
        this.path = path;
    }
    public string Path
    {
        get
        {
            if (path == null) throw new ObjectDisposedException(GetType().Name);
            return path;
        }
    }
    ~TempFile() { Dispose(false); }
    public void Dispose() { Dispose(true); }
    private void Dispose(bool disposing)
    {
        if (disposing)
        {
            GC.SuppressFinalize(this);                
        }
        if (path != null)
        {
            try { File.Delete(path); }
            catch { } // best effort
            path = null;
        }
    }
}