using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace AE.Net.Mail
{
    enum ReadState
    {
        LINEDATA,
        CR,
        EOF,
        EXCESS,
    }

    public class LineStream : IDisposable
    {
        Stream stream;
        Encoding encoding;
        MemoryStream lineBuffer;
        ReadState readState;
        public bool Debug;

        public LineStream(Stream stream, Encoding encoding)
        {
            this.stream = stream;
            this.encoding = encoding;
            this.lineBuffer = new MemoryStream();
        }

        public string ReadLine(int timeout = -1)
        {
            int maxLength = int.MaxValue;
            return ReadLine(ref maxLength, timeout);
        }

        private string TakeCurrentLine(ref int bufferSize)
        {
            var bytes = lineBuffer.ToArray();
            bufferSize -= bytes.Length;
            var line = encoding.GetString(bytes);
            if (Debug)
                Trace.WriteLine("S:" + line);
            lineBuffer = new MemoryStream();
            return line;
        }

        private readonly byte[] NEWLINE = new byte[] { 13, 10 };

        public void WriteLine(string line)
        {
            if (Debug)
                Trace.WriteLine("C:" + line);
            _WriteRawLine(encoding.GetBytes(line));
        }

        public void WriteRawLine(byte[] bytes)
        {
            if (Debug)
                Trace.WriteLine("C: (WRITE RAW [" + bytes.Length.ToString() + "] BYTES)");
            _WriteRawLine(bytes);
        }

        private void _WriteRawLine(byte[] bytes)
        {
            stream.Write(bytes, 0, bytes.Length);
            WriteLineEnding();
        }

        private void WriteLineEnding()
        {
            stream.Write(NEWLINE, 0, NEWLINE.Length);
        }

        public string ReadLine(ref int maxLength, int timeout = -1)
        {
            stream.ReadTimeout = timeout;
            if (readState == ReadState.EOF)
                return null;
            while (true)
            {
                int b = stream.ReadByte();
                if (b == -1)
                {
                    readState = ReadState.EOF;
                    return TakeCurrentLine(ref maxLength);
                }
                if (b == 10)
                {
                    if (readState == ReadState.CR || readState == ReadState.EXCESS)
                    {
                        readState = ReadState.LINEDATA;
                        continue;
                    }
                    readState = ReadState.LINEDATA;
                    return TakeCurrentLine(ref maxLength);
                }
                else if (readState != ReadState.EXCESS)
                {
                    readState = ReadState.LINEDATA;
                }
                if (b == 13)
                {
                    if (readState == ReadState.EXCESS)
                    {
                        readState = ReadState.CR;
                        continue;
                    }
                    readState = ReadState.CR;
                    return TakeCurrentLine(ref maxLength);
                }
                if (readState == ReadState.EXCESS)
                    continue;
                lineBuffer.WriteByte((byte)b);
                if (lineBuffer.Length == maxLength)
                {
                    readState = ReadState.EXCESS;
                    return TakeCurrentLine(ref maxLength);
                }
            }
        }

        public void Dispose()
        {
            stream.Dispose();
        }

        internal string ReadToEnd(int maxLength, Encoding Encoding)
        {
            throw new NotImplementedException();
        }

        public byte[] ConsumeRaw(int size)
        {
            if (Debug)
                Trace.WriteLine("S: (CONSUME RAW [" + size.ToString() + "] BYTES)");
            int eol = -1;
            if (readState == ReadState.CR)
                eol = stream.ReadByte();
            byte[] buffer = new byte[size];
            int cursor = 0;
            if (eol != -1)
            {
                buffer[0] = (byte)eol;
                cursor++;
            }
            int len;
            while (0 < (len = stream.Read(buffer, cursor, buffer.Length - cursor)))
                ;
            return buffer;
        }

        public Encoding Encoding { get { return this.encoding; } }
    }
}
