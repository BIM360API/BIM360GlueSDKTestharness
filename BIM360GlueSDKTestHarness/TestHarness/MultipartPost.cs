// Copyright 2012 Autodesk, Inc.  All rights reserved.
// Use of this software is subject to the terms of the Autodesk license agreement 
// provided at the time of installation or download, or which otherwise accompanies 
// this software in either electronic or hard copy form.   

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace BIM360SDKTestClient
{
  public class MultipartPostData
  {
    // Change this if you need to, not necessary
    public static string boundary = "AaB03x";
    public static string lineEnding = "\r\n";

    private List<PostDataParam> m_Params;

    public List<PostDataParam> Params
    {
      get { return m_Params; }
      set { m_Params = value; }
    }

    public MultipartPostData()
    {
      m_Params = new List<PostDataParam>();
    }

    //
    // Returns the parameters array formatted for multi-part/form data
    //
    public string GetPostDataORIG()
    {
      StringBuilder sb = new StringBuilder();
      foreach (PostDataParam p in m_Params)
      {
        sb.AppendLine("--" + boundary);

        if (p.Type == PostDataParamType.File)
        {
          sb.AppendLine(string.Format("Content-Disposition: file; name=\"{0}\"; filename=\"{1}\"", p.Name, p.FileName));
          sb.AppendLine("Content-Type: application/octet-stream");
          sb.AppendLine();
          //sb.AppendLine(p.Value);
        }
        else
        {
          sb.AppendLine(string.Format("Content-Disposition: form-data; name=\"{0}\"", p.Name));
          sb.AppendLine();
          //sb.AppendLine(p.Value);
        }
      }

      sb.AppendLine("--" + boundary + "--");

      return sb.ToString();
    }

    private int getLen(string aString)
    {
      return aString.Length;
    }

    private int appendToBuffer(byte[] inBuffer, int offset, string newLine)
    {
      Encoding.UTF8.GetBytes(newLine).CopyTo(inBuffer, offset);
      offset += newLine.Length;
      return offset;
    }

    //
    // Returns the parameters array formatted for multi-part/form data
    //
    public byte[] GetPostData()
    {
      int bufferLen = 0;
      foreach (PostDataParam p in m_Params)
      {
        bufferLen += getLen("--" + boundary + lineEnding);
        if (p.Type == PostDataParamType.File)
        {
          bufferLen += getLen(string.Format("Content-Disposition: file; name=\"{0}\"; filename=\"{1}\"", p.Name, p.FileName) + lineEnding);
          bufferLen += getLen("Content-Type: application/octet-stream" + lineEnding);
          bufferLen += getLen(lineEnding);
          bufferLen += p.Value.Length + getLen(lineEnding);
        }
        else
        {
          bufferLen += getLen(string.Format("Content-Disposition: form-data; name=\"{0}\"", p.Name) + lineEnding);
          bufferLen += getLen(lineEnding);
          bufferLen += p.Value.Length + getLen(lineEnding);
        }
      }
      bufferLen += getLen("--" + boundary + "--" + lineEnding);

      
      // Now build the buffer
      int offset = 0;
      byte[] newBuffer = new byte[bufferLen];
      foreach (PostDataParam p in m_Params)
      {
        offset = appendToBuffer(newBuffer, offset, "--" + boundary + lineEnding);

        if (p.Type == PostDataParamType.File)
        {
          offset = appendToBuffer(newBuffer, offset, string.Format("Content-Disposition: file; name=\"{0}\"; filename=\"{1}\"", p.Name, p.FileName) + lineEnding);
          offset = appendToBuffer(newBuffer, offset, "Content-Type: application/octet-stream" + lineEnding);
          offset = appendToBuffer(newBuffer, offset, lineEnding);
          Buffer.BlockCopy(p.Value, 0, newBuffer, offset, p.Value.Length);
          offset += p.Value.Length;
          offset = appendToBuffer(newBuffer, offset, lineEnding);
        }
        else
        {
          offset = appendToBuffer(newBuffer, offset, string.Format("Content-Disposition: form-data; name=\"{0}\"", p.Name) + lineEnding);
          offset = appendToBuffer(newBuffer, offset, lineEnding);
          Buffer.BlockCopy(p.Value, 0, newBuffer, offset, p.Value.Length);
          offset += p.Value.Length;
          offset = appendToBuffer(newBuffer, offset, lineEnding);
        }
      }
      offset = appendToBuffer(newBuffer, offset, "--" + boundary + "--" + lineEnding);
      return newBuffer;
    }

  }

  public enum PostDataParamType
  {
    Field,
    File
  }

  public class PostDataParam
  {
    public PostDataParam(string name, string value, PostDataParamType type)
    {
      Name = name;
      Value = Encoding.UTF8.GetBytes(value);
      Type = type;
    }

    public PostDataParam(string name, string filename, byte[] value, PostDataParamType type)
    {
      Name = name;
      if (value == null)
      {
        Value = Encoding.UTF8.GetBytes("");
      }
      else
      {
        Value = value;
      }
      FileName = filename;
      Type = type;
    }

    public string Name;
    public string FileName;
    public byte[] Value;
    public PostDataParamType Type;
  }
}
