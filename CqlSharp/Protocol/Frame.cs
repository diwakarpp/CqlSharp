﻿// CqlSharp - CqlSharp
// Copyright (c) 2013 Joost Reuzel
//   
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//   
// http://www.apache.org/licenses/LICENSE-2.0
//  
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using CqlSharp.Network.Snappy;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CqlSharp.Protocol
{
    /// <summary>
    ///   A Cassandra protocol data packet
    /// </summary>
    internal abstract class Frame : IDisposable
    {
        /// <summary>
        ///   Stream holding Frame content
        /// </summary>
        protected FrameReader Reader;

        private int _disposed; //0 not disposed, 1 disposed
        private static readonly SnappyCompressor Compressor = new SnappyCompressor();

        /// <summary>
        ///   Gets or sets the version.
        /// </summary>
        /// <value> The version. </value>
        public FrameVersion Version { get; protected set; }

        /// <summary>
        ///   Gets or sets the flags.
        /// </summary>
        /// <value> The flags. </value>
        public FrameFlags Flags { get; set; }

        /// <summary>
        ///   Gets or sets the stream identifier (request response pair)
        /// </summary>
        /// <value> The stream. </value>
        public sbyte Stream { get; set; }

        /// <summary>
        ///   Gets or sets the op code.
        /// </summary>
        /// <value> The op code. </value>
        public FrameOpcode OpCode { get; protected set; }


        /// <summary>
        ///   Gets or sets the length.
        /// </summary>
        /// <value> The length. </value>
        public int Length { get; protected set; }

        /// <summary>
        ///   Gets or sets the tracing id.
        /// </summary>
        /// <value> The tracing id. </value>
        public Guid? TracingId { get; protected set; }

        #region IDisposable Members

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        /// <summary>
        /// Gets the frame bytes.
        /// </summary>
        /// <returns></returns>
        public byte[] GetFrameBytes(bool compress)
        {
            using (var buffer = new MemoryStream())
            {
                buffer.WriteByte((byte)Version);
                buffer.WriteByte((byte)Flags);
                buffer.WriteByte(unchecked((byte)Stream));
                buffer.WriteByte((byte)OpCode);

                //write length placeholder
                buffer.WriteInt(0);

                //write the actual data
                //if (compress)
                //{
                //    using (var uncompressed = new MemoryStream())
                //    {
                //        WriteData(uncompressed);
                //        byte[] uncompressedData = uncompressed.ToArray();
                //        if (uncompressed.Length > 128)
                //        {
                //            int maxSize = Compressor.MaxCompressedLength(uncompressedData.Length);
                //            var compressedData = maxSize <= MemoryPool.BufferSize
                //                                     ? MemoryPool.Instance.Take()
                //                                     : new byte[maxSize];
                //            int compressedSize = Compressor.Compress(uncompressedData, 0, uncompressedData.Length,
                //                                                     compressedData);
                //            buffer.Write(compressedData, 0, compressedSize);
                //            MemoryPool.Instance.Return(compressedData);

                //            //add compression to flags
                //            Flags |= FrameFlags.Compression;
                //            buffer.Position = 1;
                //            buffer.WriteByte((byte)Flags);
                //        }
                //        else
                //        {
                //            buffer.Write(uncompressedData, 0, uncompressedData.Length);
                //        }
                //    }
                //}
                //else
                WriteData(buffer);

                //overwrite length with real value
                buffer.Position = 4;
                buffer.WriteInt((int)buffer.Length - 8);

                //reset buffer position
                buffer.Position = 0;

                //copy to the stream async
                byte[] data = buffer.ToArray();
                return data;
            }
        }

        /// <summary>
        ///   Writes the data to buffer.
        /// </summary>
        /// <param name="buffer"> The buffer. </param>
        protected abstract void WriteData(Stream buffer);

        /// <summary>
        ///   Reads a packet from a stream.
        /// </summary>
        /// <param name="stream"> The stream. </param>
        /// <returns> </returns>
        internal static async Task<Frame> FromStream(Stream stream)
        {
            //read header
            int read = 0;
            var header = new byte[8];
            while (read < 8)
                read += await stream.ReadAsync(header, read, 8 - read).ConfigureAwait(false);

            //get length
            if (BitConverter.IsLittleEndian) Array.Reverse(header, 4, 4);
            int length = BitConverter.ToInt32(header, 4);

            Frame frame;
            switch ((FrameOpcode)header[3])
            {
                case FrameOpcode.Error:
                    frame = new ErrorFrame();
                    break;
                case FrameOpcode.Ready:
                    frame = new ReadyFrame();
                    break;
                case FrameOpcode.Authenticate:
                    frame = new AuthenticateFrame();
                    break;
                case FrameOpcode.Supported:
                    frame = new SupportedFrame();
                    break;
                case FrameOpcode.Result:
                    frame = new ResultFrame();
                    break;
                case FrameOpcode.Event:
                    frame = new EventFrame();
                    break;
                default:
                    throw new ProtocolException(0, "Unexpected OpCode received.");
            }

            frame.Version = (FrameVersion)header[0];
            frame.Flags = (FrameFlags)header[1];
            frame.Stream = unchecked((sbyte)header[2]);
            frame.OpCode = (FrameOpcode)header[3];
            frame.Length = length;

            //wrap the stream in a window, that will be completely read when disposed
            var reader = new FrameReader(stream, length);
            frame.Reader = reader;

            //decompress the contents of the frame (implicity loads the entire frame body!)
            if (frame.Flags.HasFlag(FrameFlags.Compression))
                await reader.DecompressAsync();

            //read tracing id if set
            if (frame.Flags.HasFlag(FrameFlags.Tracing))
                frame.TracingId = await reader.ReadUuidAsync().ConfigureAwait(false);

            await frame.InitializeAsync().ConfigureAwait(false);

            return frame;
        }

        /// <summary>
        ///   Initialize frame contents from the stream
        /// </summary>
        /// <param name="stream"> The stream. </param>
        protected abstract Task InitializeAsync();

        /// <summary>
        ///   Completes when the frame body is read
        /// </summary>
        /// <returns> </returns>
        public virtual Task WaitOnBodyRead()
        {
            return Reader.WaitUntilFrameDataRead;
        }


        protected virtual void Dispose(bool disposing)
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
            {
                if (disposing)
                {
                    Reader.Dispose();
                    //Reader = null;
                }
            }
        }

        ~Frame()
        {
            Dispose(false);
        }
    }
}