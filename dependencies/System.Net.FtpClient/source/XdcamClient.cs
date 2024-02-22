﻿using System.Diagnostics;
using System.Text;
using System.IO;

namespace System.Net.FtpClient
{
    /// <summary>
    /// Client that supports Sony XDCAM proffessional disc drive
    /// </summary>
    public class XdcamClient : FtpClient
    {

        private readonly string _root;

        /// <summary>
        /// Creates a new isntance of XdcamClient
        /// </summary>
        public XdcamClient(Uri uri)
        {
            Host = uri.Host;
            _root = uri.AbsolutePath;
            if (!_root.EndsWith("/"))
                _root += "/";
            EnableThreadSafeDataConnections = false;
            DataConnectionType = FtpDataConnectionType.PASV;
            UngracefullDisconnection = true;
            Encoding = Encoding.UTF8;            
        }

        /// <summary>
        /// Gets the size of the file
        /// </summary>
        /// <param name="path">The full or relative path of the file</param>
        /// <returns>-1 if the command fails, otherwise the file size</returns>
        public override long GetFileSize(string path)
        {
            try
            {
                m_lock.WaitOne();
                Execute("TYPE I");
                // read in one line of raw file listing for the file - it's the only method to get file size
                try
                {
                    using (FtpDataStream stream = OpenDataStream($"LIST {path}", 0))
                    {
                        try
                        {
                            var buf = stream.ReadLine(Encoding);
                            if (!string.IsNullOrWhiteSpace(buf))
                            {
                                FtpTrace.WriteLine(buf);
                                FtpListItem itemdata = FtpListItem.ParseUnixList(buf, FtpCapability.NONE);
                                return itemdata?.Size ?? 0;
                            }
                        }
                        finally
                        {
                            stream.Close();
                        }
                    }
                }
                catch (FtpCommandException)
                {
                    return 0;
                }
            }
            finally
            {
                m_lock.ReleaseMutex();
            }
            return -1;
        }

        /// <summary>
        /// Connect to the server. Throws ObjectDisposedException if this object has been disposed.
        /// </summary>
        /// <example><code source="..\Examples\Connect.cs" lang="cs" /></example>
        public override void Connect()
        {
            Debug.WriteLine($"XDCAM Connect {Threading.Thread.CurrentThread.ManagedThreadId}");
            try
            {
                m_lock.WaitOne();

                if (IsDisposed)
                    throw new ObjectDisposedException("This FtpClient object has been disposed. It is no longer accessible.");

                if (m_stream == null)
                    m_stream = new FtpSocketStream();
                else
                    if (IsConnected)
                        Disconnect();

                if (Host == null)
                    throw new FtpException("No host has been specified");

                m_hashAlgorithms = FtpHashAlgorithm.NONE;
                m_stream.ConnectTimeout = m_connectTimeout;
                m_stream.SocketPollInterval = m_socketPollInterval;
                m_stream.Connect(Host, Port, InternetProtocolVersions);
                m_stream.SetSocketOption(Sockets.SocketOptionLevel.Socket,
                    Sockets.SocketOptionName.KeepAlive, m_keepAlive);

                FtpReply reply;
                if (!(reply = GetReply()).Success)
                {
                    if (reply.Code == null)
                    {
                        throw new IOException("The connection was terminated before a greeting could be read.");
                    }
                    else
                    {
                        throw new FtpCommandException(reply);
                    }
                }

                if (m_credentials != null)
                {
                    Authenticate();
                }
            }
            finally
            {
                m_lock.ReleaseMutex();
            }
        }

        /// <inheritdoc/>
        public override void Disconnect()
        {
            Debug.WriteLine($"XDCAM Disconnect {Threading.Thread.CurrentThread.ManagedThreadId}" );
            base.Disconnect();
            Threading.Thread.Sleep(1000); // have to wait until device notifies the disconnection
        }

        /// <inheritdoc/>
        public override Stream OpenRead(string path, FtpDataType type, long restart)
        {
            return base.OpenRead(_root + path, type, restart);
        }

        /// <inheritdoc/>
        public override Stream OpenWrite(string path, FtpDataType type)
        {
            return base.OpenWrite(_root + path, type);
        }

        /// <inheritdoc/>
        public override Stream OpenAppend(string path, FtpDataType type)
        {
            return base.OpenAppend(_root + path, type);
        }

        /// <summary>
        /// Opens the specified file for segment reading
        /// </summary>
        /// <param name="path">The full or relative path of the file</param>
        /// <param name="startFrame">The start frame number</param>
        /// <param name="frameCount">Frame count to read from the file</param>
        /// <returns>A stream for reading the file on the device</returns>
        public Stream OpenPart(string path, int startFrame, int frameCount)
        {
            try
            {
                m_lock.WaitOne();
                return OpenDataStream($"SITE REPFL \"{_root + path}\" {startFrame} {frameCount}", 0);
            }
            finally
            {
                m_lock.ReleaseMutex();
            }
        }
        /// <summary>
        /// Read free disc space from device
        /// </summary>
        /// <returns>Free disc space on the device, 0 if unknown</returns>
        public long GetFreeDiscSpace()
        {
            try
            {
                m_lock.WaitOne();
                using (Stream stream = OpenDataStream("SITE DF", 0))
                {
                    using (StreamReader reader = new StreamReader(stream, Encoding.ASCII))
                    {
                        while (!reader.EndOfStream)
                        {
                            string response = reader.ReadLine();
                            if (response?.StartsWith("others") == true)
                            {
                                return long.Parse(response.Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries)[1]);
                            }
                        }
                        return 0L;
                    }
                }
            }
            finally
            {
                m_lock.ReleaseMutex();
            }
        }

    }
    
}
